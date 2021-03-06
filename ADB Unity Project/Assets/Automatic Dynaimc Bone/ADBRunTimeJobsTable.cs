﻿
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Jobs.LowLevel;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using System;



namespace ADBRuntime.Internal
{


    public unsafe class ADBRunTimeJobsTable
    {
        #region Single
        private static ADBRunTimeJobsTable instance_ADBRunTimeJobsTable;//OYM：单例模式
        private ADBRunTimeJobsTable() { }
        public static ADBRunTimeJobsTable GetRunTimeJobsTable(bool isDebug = false)
        {
            if (instance_ADBRunTimeJobsTable == null)
            {
                instance_ADBRunTimeJobsTable = new ADBRunTimeJobsTable();
            }
            if (isDebug)
            {
                ADBRuntimeJobsTableMono mono =GameObject.Find("ADBRunTimeJobsTable")?.GetComponent<ADBRuntimeJobsTableMono>();
                if (mono == null)
                {
                    mono=new GameObject("ADBRunTimeJobsTable").AddComponent<ADBRuntimeJobsTableMono>();
                }
                instance_ADBRunTimeJobsTable.isDebug = true;
            }
            return instance_ADBRunTimeJobsTable;
        }

        #endregion
        internal JobHandle returnHJob;
        internal bool isDebug;
        // private int complexHJobBatchCount=8;
        //先预留在这里看下性能
        private const float EPSILON = 0.001f;
        public int computeCount=0;
        public void Add(int count = 1)
        {
            if (isDebug)
            {
                computeCount += count;
            }
        }

        #region Jobs
        [BurstCompile]
        public struct InitiralizeCollider : IJobParallelForTransform
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderRead* pReadColliders;
            [NativeDisableUnsafePtrRestriction]
            public ColliderReadWrite* pReadWriteColliders;

            public void Execute(int index, TransformAccess transform)
            {
/*
            }
            public void TryExecute(TransformAccessArray transforms, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < transforms.length; i++)
                {
                    Execute(i, transforms[i]);
                }
            }
            public void Execute(int index, Transform transform)
            {
*/
                ColliderReadWrite* pReadWriteCollider = pReadWriteColliders + index;
                ColliderRead* pReadCollider = pReadColliders + index;

                pReadWriteCollider->position = pReadWriteCollider->positionForward = transform.position + transform.rotation * pReadCollider->positionOffset;
                pReadWriteCollider->direction = pReadWriteCollider->directionForward = transform.rotation * pReadCollider->staticDirection;
                pReadWriteCollider->normal = pReadWriteCollider->normalForward = transform.rotation * pReadCollider->staticNormal;
            }
        }
        [BurstCompile]
        public struct InitiralizePoint : IJobParallelForTransform

        {
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public PointRead* pReadPoints;
            [NativeDisableUnsafePtrRestriction]
            public PointReadWrite* pReadWritePoints;

            public void Execute(int index, TransformAccess transform)
            {
                /*
            }
            public void TryExecute(TransformAccessArray transforms, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < transforms.length; i++)
                {
                    Execute(i, transforms[i]);
                }
            }
            void Execute(int index, Transform transform)
            {
            */
                var pReadWritePoint = pReadWritePoints + index;
                var pReadPoint = pReadPoints + index;


                if ((pReadPoints + index)->isVirtual)//OYM：virtual point
                {
                    (pReadWritePoints + index)->position = transform.position + Vector3.down * 0.1f;
                }
                else if (pReadPoint->fixedIndex == index)
                {
                    (pReadWritePoints + index)->position = transform.position;
                }
                else
                {
                    var pFixReadWritePoint = pReadWritePoints + (pReadPoint->fixedIndex);
                    pReadWritePoint->position = pFixReadWritePoint->position + pReadPoint->initialPosition;
                }
            }
        }
        [BurstCompile]
        public struct PointGetTransform : IJobParallelForTransform
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public PointRead* pReadPoints;
            [NativeDisableUnsafePtrRestriction]
            public PointReadWrite* pReadWritePoints;
            [ReadOnly]
            public int iteration;
            public void Execute(int index, TransformAccess transform)
            {
                /*
            }
            public void TryExecute(TransformAccessArray transforms, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < transforms.length; i++)
                {
                    Execute(i, transforms[i]);
                }
            }

            public void Execute(int index, Transform transform)
            {
            */
                PointRead* pReadPoint = pReadPoints + index;
                PointRead* pFixedPointRead = (pReadPoints + pReadPoint->fixedIndex);
                PointReadWrite* pReadWritePoint = pReadWritePoints + index;
                PointReadWrite* pFixedPointReadWrite = (pReadWritePoints + pReadPoint->fixedIndex);
                (pReadWritePoint)->velocity *= pReadPoint->mass;

                if (pReadPoint->fixedIndex == index)//OYM：fixedpoint
                {
                    pReadWritePoint->velocity = transform.position - (pReadWritePoints + index)->position;
                    pReadWritePoint->position = transform.position;
                }
                else
                {
                
                    Vector3 move = pFixedPointReadWrite->velocity * pReadPoint->moveByFixedPoint;
                    pReadWritePoint->position += pFixedPointReadWrite->velocity * pReadPoint->distanceCompensation;
                    Vector3 freezeVector = ( pReadWritePoint->position- pFixedPointReadWrite->position)-pReadPoint->initialPosition;
                    pReadWritePoint->position += pFixedPointRead->freeze* freezeVector;
                    pReadWritePoint->velocity -= move;
                }
            }
            float Lerp(float a, float b, float t)
            {
                t = t < 0 ? 0 : (t > 1 ? 1 : t);
                return a * (1 - t) + b * t;
            }
        }
        [BurstCompile]
        public struct ColliderGetTransform : IJobParallelForTransform
        //OYM：把job的点转换成实际的点
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderRead* pReadColliders;
            [NativeDisableUnsafePtrRestriction]
            public ColliderReadWrite* pReadWriteColliders;

            public void Execute(int index, TransformAccess transform)
            {
                ColliderReadWrite* pReadWriteCollider = pReadWriteColliders + index;
                ColliderRead* pReadCollider = pReadColliders + index;

                switch (pReadCollider->colliderType)
                {
                    case ColliderType.Sphere:
                        pReadWriteCollider->positionForward = transform.position + transform.rotation * pReadCollider->positionOffset;
                        break;
                    case ColliderType.Capsule:
                        pReadWriteCollider->positionForward = transform.position + transform.rotation * pReadCollider->positionOffset;
                        pReadWriteCollider->directionForward = transform.rotation * pReadCollider->staticDirection;
                        break;
                    case ColliderType.OBB:
                        pReadWriteCollider->positionForward = transform.position + transform.rotation * pReadCollider->positionOffset;
                        pReadWriteCollider->normalForward = transform.rotation * pReadCollider->staticNormal;
                        break;
                    default:
                        break;
                }
            }
        }
        [BurstCompile]
        public struct ColliderUpdate : IJobParallelFor
        //OYM：把job的点转换成实际的点
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderRead* pReadColliders;
            [NativeDisableUnsafePtrRestriction]
            public ColliderReadWrite* pReadWriteColliders;
            [ReadOnly]
            internal float step;

            public void Execute(int index)
            {
                ColliderReadWrite* pReadWriteCollider = pReadWriteColliders + index;
                ColliderRead* pReadCollider = pReadColliders + index;

                switch (pReadCollider->colliderType)
                {
                    case ColliderType.Sphere:
                        pReadWriteCollider->position = Vector3.Lerp(pReadWriteCollider->position, pReadWriteCollider->positionForward, step);
                        break;
                    case ColliderType.Capsule:
                        pReadWriteCollider->position = Vector3.Lerp(pReadWriteCollider->position, pReadWriteCollider->positionForward, step);
                        pReadWriteCollider->direction = Vector3.Lerp(pReadWriteCollider->direction, pReadWriteCollider->directionForward, step);
                        break;
                    case ColliderType.OBB:
                        pReadWriteCollider->position = Vector3.Lerp(pReadWriteCollider->position, pReadWriteCollider->positionForward, step);
                        pReadWriteCollider->normal = Quaternion.Lerp(pReadWriteCollider->normal, pReadWriteCollider->normalForward, step);
                        break;
                    default:
                        break;
                }
            }
        }
        [BurstCompile]
        public struct PointUpdate : IJobParallelFor
        {

            [NativeDisableUnsafePtrRestriction]
            internal PointReadWrite* pReadWritePoints;
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            internal PointRead* pReadPoints;
            [ReadOnly]
            internal Vector3 windForcePower;
            [ReadOnly]
            internal float scale;
            [ReadOnly]
            internal float deltaTime;
            [ReadOnly]
            internal float iteration;
            public void TryExecute(int index, int _, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < index; i++)
                {
                    Execute(i);
                }
            }
            //OYM：
            public void Execute(int index)
            {
                PointRead* pReadPoint = pReadPoints + index;
                if (pReadPoint->fixedIndex != index)
                {
                    PointReadWrite* pReadWritePoint = pReadWritePoints + index;
                    PointReadWrite* pParentReadWritePoint = pReadWritePoints + (pReadPoint->parent);

                    pReadWritePoint->velocity += pReadPoint->gravity * scale * (0.5f * deltaTime * deltaTime) / iteration;//OYM：重力(要计算iteration次所以除以个iteration)
                    pReadWritePoint->velocity += windForcePower*pReadPoint->windScale / (iteration * pReadPoint->weight);
                    pReadWritePoint->position += pReadWritePoint->velocity / iteration;
                }
            }
        }
        [BurstCompile]
        public struct JobConstraintUpdate : IJobParallelFor
        {
            /// <summary>
            /// 指向所有可读的点
            /// </summary>);
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public PointRead* pReadPoints;
            /// <summary>
            /// 指向所有可读写的点
            /// </summary>);
            [NativeDisableUnsafePtrRestriction]
            public PointReadWrite* pReadWritePoints;
            /// <summary>
            /// 所有可读的碰撞体
            /// </summary>);
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderRead* pReadColliders;

            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderReadWrite* pReadWriteColliders;

            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderRead* pGlobalReadColliders;

            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderReadWrite* pGlobalReadWriteColliders;

            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ConstraintRead* pConstraintsRead;
            [ReadOnly]
            /// <summary>
            /// 碰撞体序号
            /// </summary>);
            public int colliderCount;
            [ReadOnly]
            public float scale;
            [ReadOnly]
            public int globalColliderCount;
            [ReadOnly]
            public bool isCollision;

            public void TryExecute(int index, int temp, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < index; i++)
                {
                    Execute(i);
                }
            }
            public void Execute(int index)
            {

                // public void Executea(int index)

                //OYM：获取约束
                ConstraintRead* constraint = pConstraintsRead + index;

                //OYM：获取约束的节点AB
                PointRead* pPointReadA = pReadPoints + constraint->indexA;
                PointRead* pPointReadB = pReadPoints + constraint->indexB;

                //OYM：任意一点都不能小于极小值
                //OYM：if ((WeightA <= EPSILON) && (WeightB <= EPSILON))
                //OYM：获取可读写的点A
                PointReadWrite* pReadWritePointA = pReadWritePoints + constraint->indexA;

                //OYM：获取可读写的点B
                PointReadWrite* pReadWritePointB = pReadWritePoints + constraint->indexB;
                //OYM：获取约束的朝向
                var Direction = pReadWritePointB->position - pReadWritePointA->position;


                float Distance = Direction.magnitude;
                //OYM：力度等于距离减去长度除以弹性，这个值可以不存在，可以大于1但是没有什么卵用
                float Force = (Distance - constraint->length * scale);
                //OYM：是否收缩，意味着力大于0
                bool IsShrink = Force >= 0.0f;
                float ConstraintPower;//OYM：这个值等于
                switch (constraint->type)
                //OYM：这下面都是一个意思，就是确认约束受到的力，然后根据这个获取杆件约束的属性，计算 ConstraintPower
                //OYM：Shrink为杆件全局值，另外两个值为线性插值获取的值，同理Stretch，所以这里大概可以猜中只是一个简单的不大于1的值
                {
                    case ConstraintType.Structural_Vertical:
                        ConstraintPower = IsShrink
                            ? constraint->shrink * (pPointReadA->structuralShrinkVertical + pPointReadB->structuralShrinkVertical)
                            : constraint->stretch * (pPointReadA->structuralStretchVertical + pPointReadB->structuralStretchVertical);
                        break;
                    case ConstraintType.Structural_Horizontal:
                        ConstraintPower = IsShrink
                            ? constraint->shrink * (pPointReadA->structuralShrinkHorizontal + pPointReadB->structuralShrinkHorizontal)
                            : constraint->stretch * (pPointReadA->structuralStretchHorizontal + pPointReadB->structuralStretchHorizontal);
                        break;
                    case ConstraintType.Shear:
                        ConstraintPower = IsShrink
                            ? constraint->shrink * (pPointReadA->shearShrink + pPointReadB->shearShrink)
                            : constraint->stretch * (pPointReadA->shearStretch + pPointReadB->shearStretch);
                        break;
                    case ConstraintType.Bending_Vertical:
                        ConstraintPower = IsShrink
                            ? constraint->shrink * (pPointReadA->bendingShrinkVertical + pPointReadB->bendingShrinkVertical)
                            : constraint->stretch * (pPointReadA->bendingStretchVertical + pPointReadB->bendingStretchVertical);
                        break;
                    case ConstraintType.Bending_Horizontal:
                        ConstraintPower = IsShrink
                            ? constraint->shrink * (pPointReadA->bendingShrinkHorizontal + pPointReadB->bendingShrinkHorizontal)
                            : constraint->stretch * (pPointReadA->bendingStretchHorizontal + pPointReadB->bendingStretchHorizontal);
                        break;
                    case ConstraintType.Circumference:
                        ConstraintPower = IsShrink
                            ? constraint->shrink * (pPointReadA->circumferenceShrink + pPointReadB->circumferenceShrink)
                            : constraint->stretch * (pPointReadA->circumferenceStretch + pPointReadB->circumferenceStretch);
                        break;
                    case ConstraintType.Virtual:
                        ConstraintPower = 1;
                        break;
                    default:
                        ConstraintPower = 0.0f;
                        break;
                }


                //OYM：获取AB点重量比值

                float WeightProportion = pPointReadB->weight / (pPointReadA->weight + pPointReadB->weight);

                if (ConstraintPower > 0.0f)//OYM：这里不可能小于0吧（除非有人搞破坏）
                {
                    Vector3 Displacement = Direction.normalized * (Force * ConstraintPower);

                    pReadWritePointA->position += Displacement * WeightProportion;
                    pReadWritePointA->velocity += Displacement * WeightProportion;
                    pReadWritePointB->position -= Displacement * (1 - WeightProportion);
                    pReadWritePointB->velocity -= Displacement * (1 - WeightProportion);

                }

                if (isCollision && constraint->isCollider)
                {
                    for (int i = 0; i < colliderCount; ++i)
                    {
                        ColliderRead* pReadCollider = pReadColliders + i;//OYM：终于到碰撞这里了

                        if (pReadCollider->isOpen&& (pPointReadA->colliderChoice & pReadCollider->colliderChoice)!=0)
                        {//OYM：collider是否打开,且pPointReadA->colliderChoice是否包含 pReadCollider->colliderChoice的位
                            ColliderReadWrite* pReadWriteCollider = pReadWriteColliders + i;
                            ComputeCollider(pReadCollider, pReadWriteCollider, pReadWritePointA, pReadWritePointB, WeightProportion, pPointReadA->friction,pPointReadB->friction);
                        }
                    }
                }
            }

            private void ComputeCollider(ColliderRead* pReadCollider, ColliderReadWrite* pReadWriteCollider, PointReadWrite* pReadWritePointA, PointReadWrite* pReadWritePointB, float WeightProportion,
                float frictionA,float frictionB)
            {
                float throwTemp;
                float t;
                switch (pReadCollider->colliderType)
                {
                    case ColliderType.Sphere:
                        {
                            Vector3 pointOnLine = ConstrainToSegment(pReadWriteCollider->position, pReadWritePointA->position, pReadWritePointB->position - pReadWritePointA->position, out t);
                            DistributionPower(pointOnLine - pReadWriteCollider->position, pReadCollider->radius, pReadWritePointA, pReadWritePointB, WeightProportion, t,frictionA,frictionB, pReadCollider->collideFunc);
                        }

                        break;
                    case ColliderType.Capsule:
                        {
                            Vector3 pointOnCollider, pointOnLine;
                            SqrComputeNearestPoints(pReadWriteCollider->position, pReadWriteCollider->direction, pReadWritePointA->position, pReadWritePointB->position - pReadWritePointA->position, out throwTemp, out t, out  pointOnCollider, out  pointOnLine);
                            DistributionPower(pointOnLine - pointOnCollider, pReadCollider->radius, pReadWritePointA, pReadWritePointB, WeightProportion, t,frictionA,frictionB, pReadCollider->collideFunc);
                        }

                        break;
                    case ColliderType.OBB:
                        {
                            float t1, t2;
                            SegmentToOBB(pReadWritePointA->position, pReadWritePointB->position, pReadWriteCollider->position, -pReadCollider->boxSize, pReadCollider->boxSize, Quaternion.Inverse(pReadWriteCollider->normal), out  t1, out t2);
                            Vector3 dir = pReadWritePointB->position - pReadWritePointA->position;
                            t1 = Clamp01(t1);
                            t2 = Clamp01(t2);
                            Vector3 pointOnLineA = pReadWritePointA->position + dir * t1;
                            Vector3 pointOnLineB = pReadWritePointA->position + dir * t2;
                            bool bHit = t1 >= 0f && t2 > t1 && t2 <= 1.0f;
                            if (bHit)
                            {
                                 t = (t1 + t2) * 0.5f;
                                Vector3 nearestPoint = pReadWritePointA->position + dir * t;
                                Vector3 pushout = Quaternion.Inverse(pReadWriteCollider->normal) * (nearestPoint - pReadWriteCollider->position);
                                float pushoutX = pushout.x > 0 ? pReadCollider->boxSize.x - pushout.x : -pReadCollider->boxSize.x - pushout.x;
                                float pushoutY = pushout.y > 0 ? pReadCollider->boxSize.y - pushout.y : -pReadCollider->boxSize.y - pushout.y;
                                float pushoutZ = pushout.z > 0 ? pReadCollider->boxSize.z - pushout.z : -pReadCollider->boxSize.z - pushout.z;
                                //OYM：这里我自己都不太记得了 XD
                                //OYM：还是不写Insideblablabla了
                                if (Abs(pushoutZ) < Abs(pushoutY) && Abs(pushoutZ) < Abs(pushoutX))
                                {
                                    pushout = pReadWriteCollider->normal * new Vector3(0, 0, pushoutZ);

                                }
                                else if (Abs(pushoutY) < Abs(pushoutX) && Abs(pushoutY) < Abs(pushoutZ))
                                {
                                    pushout = pReadWriteCollider->normal * new Vector3(0, pushoutY, 0);
                                }
                                else
                                {
                                    pushout = pReadWriteCollider->normal * new Vector3(pushoutX, 0, 0);
                                }
                                if (pushout.sqrMagnitude != 0)
                                {
                                    float inverse1Velocity = Vector3.Dot(pushout, pReadWritePointA->velocity) / pushout.sqrMagnitude;
                                    pReadWritePointA->velocity -= pushout * inverse1Velocity;
                                    pReadWritePointB->velocity -= pushout * inverse1Velocity;
                                    pReadWritePointA->velocity *= (1 - frictionA);
                                    pReadWritePointB->velocity *= (1 - frictionB);

                                    //float Propotion = WeightProportion * t / (1 - WeightProportion - t + 2 * WeightProportion * t);
                                    if (WeightProportion > EPSILON)
                                    {
                                        pReadWritePointA->position += (pushout * t);
                                        pReadWritePointA->velocity += (pushout * t);
                                    }
                                    else
                                    {
                                        t = 1;
                                    }
                                    pReadWritePointB->position += (pushout * (1 - t));
                                    pReadWritePointB->velocity += (pushout * (1 - t));

                                }
                            }
                            break;
                        }
                    default:
                        return;

                }
            }

            void DistributionPower(Vector3 pushout, float radius, PointReadWrite* pReadWritePointA, PointReadWrite* pReadWritePointB, float WeightProportion, float lengthPropotion,float frictionA,float frictionB, CollideFunc collideFunc)
            {

                float sqrPushout = pushout.sqrMagnitude;
                switch (collideFunc)
                {
                    case CollideFunc.Outside:
                        if (!(sqrPushout < radius * radius && sqrPushout != 0))
                        { return; }
                        break;
                    case CollideFunc.Inside:
                        if (sqrPushout < radius * radius && sqrPushout != 0)
                        { return; }
                        break;
                    case CollideFunc.Freeze:
                        break;                        
                }
                //OYM：把pushout方向多余的力给减掉
                pReadWritePointA->velocity -= pushout * (Vector3.Dot(pushout, pReadWritePointA->velocity) / sqrPushout);
                pReadWritePointB->velocity -= pushout * (Vector3.Dot(pushout, pReadWritePointB->velocity) / sqrPushout);
                pReadWritePointA->velocity *= (1 - frictionA);
                pReadWritePointB->velocity *= (1 - frictionB);

                pushout = pushout * (radius / Mathf.Sqrt(sqrPushout) - 1);
                //  float Propotion = WeightProportion * lengthPropotion / (1 - WeightProportion - lengthPropotion + 2 * WeightProportion * lengthPropotion);
                if (WeightProportion < EPSILON)
                {
                    pReadWritePointA->position += (pushout * (1 - lengthPropotion));
                    pReadWritePointA->velocity += (pushout * (1 - lengthPropotion));
                }
                else
                {
                    lengthPropotion = 1;
                }
                pReadWritePointB->position += (pushout * lengthPropotion);
                pReadWritePointB->velocity += (pushout * lengthPropotion);
            }
            //OYM：https://zalo.github.io/blog/closest-point-between-segments/#line-segments
            //OYM：目前是我见过最快的方法
            float SqrComputeNearestPoints(
                Vector3 posP,//OYM：碰撞体的位置起点位置
                Vector3 dirP,//OYM：碰撞体的朝向
                Vector3 posQ,//OYM：约束的起点坐标
                Vector3 dirQ,//OYM：约束的起点朝向
out float tP, out float tQ, out Vector3 pointOnP, out Vector3 pointOnQ)
            {
                float lineDirSqrMag = dirQ.sqrMagnitude;
                Vector3 inPlaneA = posP - ((Vector3.Dot(posP - posQ, dirQ) / lineDirSqrMag) * dirQ);
                Vector3 inPlaneB = posP + dirP - ((Vector3.Dot(posP + dirP - posQ, dirQ) / lineDirSqrMag) * dirQ);
                Vector3 inPlaneBA = inPlaneB - inPlaneA;

                float t1 = Vector3.Dot(posQ - inPlaneA, inPlaneBA) / inPlaneBA.sqrMagnitude;
                t1 = (inPlaneA != inPlaneB) ? t1 : 0f; // Zero's t if parallel
                Vector3 L1ToL2Line = posP + dirP * Clamp01(t1);

                pointOnQ = ConstrainToSegment(L1ToL2Line, posQ, dirQ, out tQ);
                pointOnP = ConstrainToSegment(pointOnQ, posP, dirP, out tP);
                return (pointOnP - pointOnQ).sqrMagnitude;
            }

            Vector3 ConstrainToSegment(Vector3 tag, Vector3 pos, Vector3 dir, out float t)
            {
                t = Vector3.Dot(tag - pos, dir) / dir.sqrMagnitude;
                t = Clamp01(t);
                return pos + dir * t;
            }
            void SegmentToOBB(Vector3 start, Vector3 end, Vector3 center, Vector3 min, Vector3 max, Quaternion InverseNormal, out float t1, out float t2)
            {
                Vector3 startP = InverseNormal * (center - start);
                Vector3 endP = InverseNormal * (center - end);
                SegmentToAABB(startP, endP, center, min, max, out t1, out t2);
            }

            void SegmentToAABB(Vector3 start, Vector3 end, Vector3 center, Vector3 min, Vector3 max, out float t1, out float t2)
            {
                Vector3 dir = end - start;
                t1 = Max(Min((min.x - start.x) / dir.x, (max.x - start.x) / dir.x), Min((min.y - start.y) / dir.y, (max.y - start.y) / dir.y), Min((min.z - start.z) / dir.z, (max.z - start.z) / dir.z));
                t2 = Min(Max((min.x - start.x) / dir.x, (max.x - start.x) / dir.x), Max((min.y - start.y) / dir.y, (max.y - start.y) / dir.y), Max((min.z - start.z) / dir.z, (max.z - start.z) / dir.z));
            }
            float Abs(float A)
            {
                return A > 0 ? A : -A;
            }
            float Clamp01(float A)
            {
                return A > 0 ? (A < 1 ? A : 1) : 0;
            }
            float Min(float A, float B, float C)
            {
                return A < B ? (A < C ? A : C) : (B < C ? B : C);
            }
            float Min(float A, float B)
            {
                return A > B ? B : A;
            }
            float Max(float A, float B, float C)
            {
                return A > B ? (A > C ? A : C) : (B > C ? B : C);
            }
            float Max(float A, float B)
            {
                return A > B ? A : B;
            }
        }
        [BurstCompile]
        public struct JobCollisionPoint : IJobParallelFor//OYM：点碰撞
        {
            /// <summary>
            /// 所有可读写的点的指针
            /// </summary>);
            [NativeDisableUnsafePtrRestriction]
            public PointReadWrite* pReadWritePoints;
            [NativeDisableUnsafePtrRestriction]
            public PointRead* pReadPoints;
            /// <summary>
            /// 所有碰撞体的指针
            /// </summary>);
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderReadWrite* pReadWriteColliders;
            /// <summary>
            /// 所有碰撞体坐标的指针
            /// </summary>);
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public ColliderRead* pReadColliders;
            [ReadOnly]
            public int colliderCount;
            [ReadOnly]
            internal bool isCollider;
            [ReadOnly]
            internal float step;
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            internal ColliderRead* pGlobalReadColliders;
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            internal ColliderReadWrite* pGlobalReadWriteColliders;
            internal int globalColliderCount;
            public void TryExecute(int index, int temp, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < index; i++)
                {
                    Execute(i);
                }
            }
            public void Execute(int index)
            {
                if (!isCollider) return;
                float throwTemp;
                var pReadWritePoint = pReadWritePoints + index;
                var pPointRead = pReadPoints + index;
                //OYM：获取可写点
                for (int i = 0; i < colliderCount; ++i)
                {
                    ColliderRead* pReadCollider = pReadColliders + i;
                    ColliderReadWrite* pReadWriteCollider = pReadWriteColliders + i;
                    //OYM：条件判断
                    if (pReadCollider->isOpen && (pPointRead->colliderChoice & pReadCollider->colliderChoice) == 0)
                    { continue; }

                    Vector3 pushout;
                    float sqrPushout;
                    switch (pReadCollider->colliderType)
                    {
                        case ColliderType.Sphere:
                            pushout = pReadWritePoint->position - pReadWriteCollider->position;
                            sqrPushout = pushout.sqrMagnitude;
                            if (sqrPushout < pReadCollider->radius * pReadCollider->radius)
                            {
                                pushout = pushout * (pReadCollider->radius / Mathf.Sqrt(sqrPushout) - 1);
                                pReadWritePoint->position += pushout;
                                pReadWritePoint->velocity += pushout;
                            }
                            break;
                        case ColliderType.Capsule:
                            pushout = pReadWritePoint->position - ConstrainToSegment(pReadWritePoint->position, pReadWriteCollider->position, pReadWriteCollider->direction, out throwTemp);
                            sqrPushout = pushout.sqrMagnitude;
                            if (sqrPushout < pReadCollider->radius * pReadCollider->radius)
                            {
                                pushout = pushout * (pReadCollider->radius / Mathf.Sqrt(sqrPushout) - 1);
                                pReadWritePoint->position += pushout;
                                pReadWritePoint->velocity += pushout;
                            }
                            break;
                        case ColliderType.OBB:
                            pushout = Quaternion.Inverse(pReadWriteCollider->normal) * (pReadWritePoint->position - pReadWriteCollider->position);
                            if (-pReadCollider->boxSize.x < pushout.x && pushout.x < pReadCollider->boxSize.x &&
                                -pReadCollider->boxSize.y < pushout.y && pushout.y < pReadCollider->boxSize.y &&
                                -pReadCollider->boxSize.z < pushout.z && pushout.z < pReadCollider->boxSize.z
                                )
                            {
                                float pushoutX = pushout.x > 0 ? pReadCollider->boxSize.x - pushout.x : -pReadCollider->boxSize.x - pushout.x;
                                float pushoutY = pushout.y > 0 ? pReadCollider->boxSize.y - pushout.y : -pReadCollider->boxSize.y - pushout.y;
                                float pushoutZ = pushout.z > 0 ? pReadCollider->boxSize.z - pushout.z : -pReadCollider->boxSize.z - pushout.z;

                                if (Abs(pushoutZ) < Abs(pushoutY) && Abs(pushoutZ) < Abs(pushoutX))
                                {
                                    pushout = pReadWriteCollider->normal * new Vector3(0, 0, pushoutZ);

                                }
                                else if (Abs(pushoutY) < Abs(pushoutX) && Abs(pushoutY) < Abs(pushoutZ))
                                {
                                    pushout = pReadWriteCollider->normal * new Vector3(0, pushoutY, 0);
                                }
                                else
                                {
                                    pushout = pReadWriteCollider->normal * new Vector3(pushoutX, 0, 0);
                                }
                                pReadWritePoint->position += pushout;
                                pReadWritePoint->velocity += pushout;
                            }
                            break;
                        default:
                            return;
                    }
                }
            }
            Vector3 ConstrainToSegment(Vector3 tag, Vector3 pos, Vector3 dir, out float t)
            {
                t = Vector3.Dot(tag - pos, dir) / dir.sqrMagnitude;
                return pos + dir * Clamp01(t);
            }
            void SegmentToOBB(Vector3 start, Vector3 end, Vector3 center, Vector3 min, Vector3 max, Quaternion InverseNormal, out float t1, out float t2)
            {
                Vector3 startP = InverseNormal * (center - start);
                Vector3 endP = InverseNormal * (center - end);
                SegmentToAABB(startP, endP, center, min, max, out t1, out t2);
            }

            void SegmentToAABB(Vector3 start, Vector3 end, Vector3 center, Vector3 min, Vector3 max, out float t1, out float t2)
            {
                Vector3 dir = end - start;
                t1 = Max(Min((min.x - start.x) / dir.x, (max.x - start.x) / dir.x), Min((min.y - start.y) / dir.y, (max.y - start.y) / dir.y), Min((min.z - start.z) / dir.z, (max.z - start.z) / dir.z));
                t2 = Min(Max((min.x - start.x) / dir.x, (max.x - start.x) / dir.x), Max((min.y - start.y) / dir.y, (max.y - start.y) / dir.y), Max((min.z - start.z) / dir.z, (max.z - start.z) / dir.z));
            }
            float Abs(float A)
            {
                return A > 0 ? A : -A;
            }
            float Clamp01(float A)
            {
                return A > 0 ? (A < 1 ? A : 1) : 0;
            }
            float Min(float A, float B, float C)
            {
                return A < B ? (A < C ? A : C) : (B < C ? B : C);
            }
            float Min(float A, float B)
            {
                return A > B ? B : A;
            }
            float Max(float A, float B, float C)
            {
                return A > B ? (A > C ? A : C) : (B > C ? B : C);
            }
            float Max(float A, float B)
            {
                return A > B ? A : B;
            }
        }
        [BurstCompile]
        public struct JobPointToTransform2 : IJobParallelForTransform
        //OYM：把job的点转换成实际的点
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction]
            public PointRead* pReadPoints;
            [NativeDisableUnsafePtrRestriction]
            public PointReadWrite* pReadWritePoints;
            [ReadOnly]
            public float deltaTime;

            public void Execute(int index, TransformAccess transform)
            {
               /*
            }
            public void TryExecute(TransformAccessArray transforms, JobHandle job)
            {
                if (!job.IsCompleted)
                {
                    job.Complete();
                }
                for (int i = 0; i < transforms.length; i++)
                {
                    Execute(i, transforms[i]);
                }
            }
            public void Execute(int index, Transform transform)
            {
                */
                PointReadWrite* pReadWritePoint = pReadWritePoints + index;//OYM：获取每个读写点
                PointRead* pReadPoint = pReadPoints + index;//OYM：获取每个只读点

                if (!(pReadPoint->fixedIndex == index || pReadPoint->isVirtual))//OYM：不是fix点
                {
                    transform.position = pReadWritePoint->position;
                }


                if (pReadPoint->childFirstIndex > -1)
                {
                    SetRotation(index, transform, pReadWritePoint, pReadPoint);//OYM：设置旋转
                }
            }
            void SetRotation(int index, TransformAccess transform, PointReadWrite* pReadWrite, PointRead* pRead)
            {
                transform.localRotation = pRead->localRotation;
                var child = pReadWritePoints + pRead->childFirstIndex;
                var parent = pReadWritePoints + pRead->parent;
                var childRead = pReadPoints + pRead->childFirstIndex;

                Vector3 ToDirection = child->position - pReadWrite->position;//OYM：朝向等于面向子节点的方向
                Vector3 FixedDirection = parent->position - pReadWrite->position;
                if (ToDirection.sqrMagnitude > EPSILON * EPSILON)//OYM：两点不再一起
                {
                    Vector3 FromDirection;
                    if (childRead->isVirtual)
                    {
                        FromDirection = childRead->boneAxis;//OYM：将BoneAxis按照transform.rotation进行旋转
                    }
                    else 
                    {
                        FromDirection = transform.rotation * childRead->boneAxis;//OYM：将BoneAxis按照transform.rotation进行旋转
                    }

                    Quaternion AimRotation = Quaternion.FromToRotation(FromDirection, ToDirection);//OYM：我仔细考虑了下,fromto用在这里不一定是最好,但是一定是最快
                    transform.rotation = AimRotation * transform.rotation;
                }
            }
        }
        #endregion
    }
}



