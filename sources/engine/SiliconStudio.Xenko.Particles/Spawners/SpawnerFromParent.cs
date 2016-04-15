﻿// Copyright (c) 2014-2016 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using SiliconStudio.Core;
using SiliconStudio.Core.Mathematics;

namespace SiliconStudio.Xenko.Particles.Spawners
{
    /// <summary>
    /// A particle spawner which continuously spawns particles based on a condition set in a followed (parent) emitter
    /// </summary>
    /// <userdoc>
    /// A particle spawner which continuously spawns particles based on a condition set in a followed (parent) emitter
    /// </userdoc>
    [DataContract("SpawnerFromParent")]
    [Display("From parent")]
    public sealed class SpawnerFromParent : ParticleSpawner
    {
        /// <summary>
        /// Some initializers require fine control between parent and child emitters. Use the control group to assign such meta-fields.
        /// </summary>
        [DataMemberIgnore]
        private ParentControlFlag parentControlFlag = ParentControlFlag.Group00;

        /// <summary>
        /// Referenced parent emitter
        /// </summary>
        [DataMemberIgnore]
        protected ParticleEmitter Parent;

        /// <summary>
        /// Referenced parent emitter's name
        /// </summary>
        [DataMemberIgnore]
        private string parentName;

        /// <summary>
        /// <c>true</c> is the parent's name has changed or the particle system has been invalidated
        /// </summary>
        private bool isParentNameDirty = true;

        /// <summary>
        /// Carry over value is used for the fractional part when number of spawned particles this frame is not an integer
        /// </summary>
        [DataMemberIgnore]
        private float carryOver;

        /// <summary>
        /// Minimum and maximum number of particles to spawn every time the condition is met
        /// </summary>
        [DataMemberIgnore]
        private Vector2 spawnCount;

        /// <summary>
        /// Name by which to reference a followed (parent) emitter
        /// </summary>
        /// <userdoc>
        /// Name by which to reference a followed (parent) emitter
        /// </userdoc>
        [DataMember(30)]
        [Display("Parent emitter")]
        public string ParentName
        {
            get { return parentName; }
            set
            {
                parentName = value;
                isParentNameDirty = true;
            }
        }

        /// <summary>
        /// Some initializers require fine control between parent and child emitters. Use the control group to assign such meta-fields.
        /// </summary>
        [DataMember(40)]
        [Display("Spawn Control Group")]
        public ParentControlFlag ParentControlFlag
        {
            get { return parentControlFlag; }
            set
            {
                RemoveControlGroup();
                parentControlFlag = value;
                AddControlGroup();
            }
        }

        /// <summary>
        /// Removes the old required control group field from the parent emitter's pool
        /// </summary>
        private void RemoveControlGroup()
        {
            var groupIndex = (int)parentControlFlag;
            if (groupIndex >= ParticleFields.ChildrenFlags.Length)
                return;

            Parent?.RemoveRequiredField(ParticleFields.ChildrenFlags[groupIndex]);
        }

        /// <summary>
        /// Adds the required control group field to the parent emitter's pool
        /// </summary>
        private void AddControlGroup()
        {
            var groupIndex = (int)parentControlFlag;
            if (groupIndex >= ParticleFields.ChildrenFlags.Length)
                return;

            Parent?.AddRequiredField(ParticleFields.ChildrenFlags[groupIndex]);
        }

        /// <summary>
        /// Gets a field accessor to the parent emitter's spawn control field, if it exists
        /// </summary>
        /// <returns></returns>
        protected ParticleFieldAccessor<ParticleChildrenAttribute> GetSpawnControlField()
        {
            var groupIndex = (int)parentControlFlag;
            if (groupIndex >= ParticleFields.ChildrenFlags.Length)
                return ParticleFieldAccessor<ParticleChildrenAttribute>.Invalid();

            return Parent?.Pool?.GetField(ParticleFields.ChildrenFlags[groupIndex]) ?? ParticleFieldAccessor<ParticleChildrenAttribute>.Invalid();
        }

        /// <summary>
        /// <see cref="ParticleSpawnTrigger"/> provides a class which checks if the spawning condition has triggered
        /// </summary>
        [DataMember(45)]
        public ParticleSpawnTrigger ParticleSpawnTrigger { get; set; }

        /// <summary>
        /// The amount of particles this spawner will emit when the event is triggered
        /// </summary>
        /// <userdoc>
        /// The amount of particles this spawner will emit when the event is triggered
        /// </userdoc>
        [DataMember(50)]
        [Display("Particles/trigger")]
        public Vector2 SpawnCount
        {
            get { return spawnCount; }
            set
            {
                MarkAsDirty();
                spawnCount = value;
            }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public SpawnerFromParent()
        {
            spawnCount = new Vector2(2, 5);
            carryOver = 0f;
        }

        /// <inheritdoc />
        public override int GetMaxParticlesPerSecond()
        {
            return (int)Math.Ceiling(Math.Max(SpawnCount.X, SpawnCount.Y));
        }

        /// <inheritdoc />
        public unsafe override void SpawnNew(float dt, ParticleEmitter emitter)
        {
            if (isParentNameDirty)
            {
                RemoveControlGroup();

                Parent = emitter.CachedParticleSystem?.GetEmitterByName(ParentName);

                AddControlGroup();

                isParentNameDirty = false;
            }

            var spawnerState = GetUpdatedState(dt, emitter);
            if (spawnerState != SpawnerState.Active)
                return;

            // Get parent pool
            if (Parent == null) return;

            var parentPool = Parent.Pool;
            var parentParticlesCount = parentPool.LivingParticles;
            if (parentParticlesCount == 0) return;

            var spawnControlGroup = GetSpawnControlField();
            if (!spawnControlGroup.IsValid())
                return;

            ParticleSpawnTrigger?.PrepareFromPool(parentPool);

            var randomSeedFieldParent = parentPool.GetField(ParticleFields.RandomSeed);

            int totalParticlesToEmit = 0;

            foreach (var parentParticle in parentPool)
            {
                uint particlesToEmit = 0;

                var parentEventTriggered = ParticleSpawnTrigger?.HasTriggered(parentParticle) ?? false;
                if (parentEventTriggered)
                {
                    var particlesToEmitFloat = SpawnCount.X;

                    if (randomSeedFieldParent.IsValid())
                    {
                        var randSeed = parentParticle.Get(randomSeedFieldParent);

                        particlesToEmitFloat = (SpawnCount.X + (SpawnCount.Y - SpawnCount.X) * randSeed.GetFloat(0));
                    }

                    particlesToEmit = (uint) Math.Floor(particlesToEmitFloat + carryOver);
                    carryOver += (particlesToEmitFloat - particlesToEmit);
                }


                ParticleChildrenAttribute childrenAttribute = ParticleChildrenAttribute.Empty;

                childrenAttribute.ParticlesToEmit = particlesToEmit;
                totalParticlesToEmit += (int)particlesToEmit;

                (*((ParticleChildrenAttribute*)parentParticle[spawnControlGroup])) = childrenAttribute;
            }

            emitter.EmitParticles(totalParticlesToEmit);
        }

        /// <inheritdoc />
        public override void InvalidateRelations()
        {
            base.InvalidateRelations();

            RemoveControlGroup();
            
            Parent = null;
            isParentNameDirty = true;
        }
    }
}