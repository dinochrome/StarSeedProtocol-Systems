namespace Code.Services.Gameplay
{
    using System.Collections.Generic;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine;

    public class PhysicsWorld2D
    {
        private KinematicBody2D player;
        private List<KinematicBody2D> aiBodies;
        private List<KinematicBody2D> staticObjects;

        private SharedBurstData sharedData;

        private const int DEAD = 0;
        private const int AI_BEINGS_START_INDEX = 1;
        private const int MIN_BEINGS_TO_CALC_COLLISIONS = 2;

        private const int SEPARATION_ITERATIONS = 3;
        private const float SEPARATION_RELAXATION = 0.5f; // only part of overlap applied each pass
        private const float MAX_PUSH_PER_ITERATION = 0.2f; // optional clamp, in world units

        public PhysicsWorld2D(KinematicBody2D player, List<KinematicBody2D> aiBodies,
            List<KinematicBody2D> staticObjects, SharedBurstData sharedData)
        {
            this.player = player;
            this.aiBodies = aiBodies;
            this.staticObjects = staticObjects;
            this.sharedData = sharedData;
        }

        public void Step(float deltaTime)
        {
            // Job-based AI-AI and AI-static separation
            RunCollisionJobs();

            // Run sequential ai-player collisions (no race condition)
            ResolvePlayerCollisions(aiBodies, AI_BEINGS_START_INDEX, checkAlive: true);
            ResolvePlayerCollisions(staticObjects, 0, checkAlive: false);
        }

        private void RunCollisionJobs()
        {
            int totalBeings = aiBodies.Count;
            if (totalBeings < MIN_BEINGS_TO_CALC_COLLISIONS) return;

            int staticCount = staticObjects.Count;

            var aiBodiesA = new NativeArray<BodyData>(totalBeings - 1, Allocator.TempJob);
            var aiBodiesB = new NativeArray<BodyData>(totalBeings - 1, Allocator.TempJob);
            var statics = new NativeArray<BodyData>(staticCount, Allocator.TempJob);

            int aliveBeings = 0;
            for (int i = AI_BEINGS_START_INDEX; i < totalBeings; i++)
            {
                if (sharedData.alive[i] != DEAD)
                {
                    var bd = new BodyData(aiBodies[i], i);
                    aiBodiesA[aliveBeings] = bd;
                    aiBodiesB[aliveBeings] = bd;
                    aliveBeings++;
                }
            }

            for (int i = 0; i < staticCount; i++)
            {
                statics[i] = new BodyData(staticObjects[i]);
            }

            var input = aiBodiesA;
            var output = aiBodiesB;

            for (int iteration = 0; iteration < SEPARATION_ITERATIONS; iteration++)
            {
                var job = new MobSeparationJob
                {
                    mobsIn = input,
                    mobsOut = output,
                    statics = statics,
                    activeMobCount = aliveBeings,
                    relaxation = SEPARATION_RELAXATION,
                    maxPushPerIteration = MAX_PUSH_PER_ITERATION
                };

                var handle = job.Schedule(aliveBeings, 64);
                handle.Complete();

                (input, output) = (output, input);
            }

            for (int i = 0; i < aliveBeings; i++)
            {
                int index = input[i].index;
                aiBodies[index].Position = input[i].position;
            }

            aiBodiesA.Dispose();
            aiBodiesB.Dispose();
            statics.Dispose();
        }

        private void ResolvePlayerCollisions(List<KinematicBody2D> bodies, int startIndex, bool checkAlive)
        {
            for (int i = startIndex; i < bodies.Count; i++)
            {
                if (checkAlive && sharedData.alive[i] == DEAD) continue;

                var body = bodies[i];
                Vector2 delta = body.Position - player.Position;
                float dist = delta.magnitude;
                float minDist = body.radius + player.radius;

                if (dist >= minDist || dist < 0.0001f) continue;

                Vector2 dir = delta / dist;
                float overlap = minDist - dist;

                float totalRes = body.pushResistance + player.pushResistance;
                float moveA = player.pushResistance / totalRes * overlap;
                float moveB = body.pushResistance / totalRes * overlap;

                body.Position += dir * moveA;
                player.Position -= dir * moveB;

                bodies[i] = body;
            }
        }

        [BurstCompile]
        private struct MobSeparationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BodyData> mobsIn;
            public NativeArray<BodyData> mobsOut;
            [ReadOnly] public NativeArray<BodyData> statics;

            public int activeMobCount;
            public float relaxation;
            public float maxPushPerIteration;

            public void Execute(int index)
            {
                BodyData a = mobsIn[index];
                Vector2 correction = Vector2.zero;

                for (int j = 0; j < activeMobCount; j++)
                {
                    if (j == index) continue;
                    correction += Resolve(a, mobsIn[j], relaxation);
                }

                for (int j = 0; j < statics.Length; j++)
                {
                    correction += ResolveStatic(a, statics[j], relaxation);
                }

                float sqrMag = correction.sqrMagnitude;
                float maxPushSqr = maxPushPerIteration * maxPushPerIteration;
                if (sqrMag > maxPushSqr)
                {
                    correction = correction / Mathf.Sqrt(sqrMag) * maxPushPerIteration;
                }

                a.position += correction;
                mobsOut[index] = a;
            }

            private static Vector2 Resolve(BodyData a, BodyData b, float relaxation)
            {
                Vector2 delta = a.position - b.position;
                float dist = delta.magnitude;
                float minDist = a.radius + b.radius;
                if (dist >= minDist || dist < 0.0001f)
                    return Vector2.zero;

                Vector2 dir = delta / dist;
                float overlap = minDist - dist;

                float totalRes = a.pushResistance + b.pushResistance;
                float moveA = b.pushResistance / totalRes * overlap * relaxation;
                return dir * moveA;
            }

            private static Vector2 ResolveStatic(BodyData a, BodyData s, float relaxation)
            {
                Vector2 delta = a.position - s.position;
                float dist = delta.magnitude;
                float minDist = a.radius + s.radius;
                if (dist >= minDist || dist < 0.0001f) return Vector2.zero;

                Vector2 dir = delta / dist;
                float overlap = (minDist - dist) * relaxation;
                return dir * overlap;
            }
        }
        private struct BodyData
        {
            public Vector2 position;
            public float radius;
            public float pushResistance;
            public int index;

            /// <summary> Beings constructor, it requires index to operate </summary>
            /// <param name="body"></param>
            /// <param name="index"></param>
            public BodyData(KinematicBody2D body, int index)
            {
                this.index = index;
                position = body.Position;
                radius = body.radius;
                pushResistance = Mathf.Max(0.0001f, body.pushResistance);
            }

            /// <summary> Statics constructor, it doesn't require index </summary>
            /// <param name="body"></param>
            public BodyData(KinematicBody2D body)
            {
                index = 0;
                position = body.Position;
                radius = body.radius;
                pushResistance = Mathf.Max(0.0001f, body.pushResistance);
            }
        }
    }
}