﻿using Content.Server.GameObjects.Components.Fluids;
using Content.Shared.Chemistry;
using Content.Shared.Physics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Update.Internal;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Serialization;
using Robust.Shared.Timers;
using Robust.Shared.ViewVariables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Timer = Robust.Shared.Timers.Timer;

namespace Content.Server.GameObjects.Components.Chemistry
{
    [RegisterComponent]
    class VaporComponent : Component, ICollideBehavior
    {
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager = default!;
#pragma warning enable 649
        public override string Name => "Vapor";

        [ViewVariables]
        private SolutionComponent _contents;
        [ViewVariables]
        private ReagentUnit _transferAmount;

        private bool _running;
        private Vector2 _direction;
        private float _velocity;


        public override void Initialize()
        {
            base.Initialize();
            _contents = Owner.GetComponent<SolutionComponent>();
        }

        public void Start(Vector2 dir, float velocity)
        {
            _running = true;
            _direction = dir;
            _velocity = velocity;
            // Set Move
            if (Owner.TryGetComponent(out ICollidableComponent collidable))
            {
                var controller = collidable.EnsureController<VaporController>();
                controller.Move(_direction, _velocity);
            }
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _transferAmount, "transferAmount", ReagentUnit.New(0.5));
        }

        public void Update()
        {
            if (!_running)
                return;

            // Get all intersecting tiles with the vapor and spray the divided solution on there
            if (Owner.TryGetComponent(out ICollidableComponent collidable))
            {
                var worldBounds = collidable.WorldAABB;
                var mapGrid = _mapManager.GetGrid(Owner.Transform.GridID);
                
                var tiles = mapGrid.GetTilesIntersecting(worldBounds);
                var amount = _transferAmount / ReagentUnit.New(tiles.Count());
                foreach (var tile in tiles)
                {
                    var pos = tile.GridIndices.ToGridCoordinates(_mapManager, tile.GridIndex);
                    SpillHelper.SpillAt(pos, _contents.SplitSolution(amount), "PuddleSmear", false); //make non PuddleSmear?
                }
            }

            if (_contents.CurrentVolume == 0)
            {
                // Delete this
                Owner.Delete();
            }
        }

        internal bool TryAddSolution(Solution solution)
        {
            if (solution.TotalVolume == 0)
            {
                return false;
            }
            var result = _contents.TryAddSolution(solution);
            if (!result)
            {
                return false;
            }

            return true;
        }

        void ICollideBehavior.CollideWith(IEntity collidedWith)
        {
            // Check for collision with a impassable object (e.g. wall) and stop
            if (collidedWith.TryGetComponent(out ICollidableComponent collidable))
            {
                if ((collidable.CollisionLayer & (int) CollisionGroup.Impassable) != 0 && collidable.Hard)
                {
                    if (Owner.TryGetComponent(out ICollidableComponent coll))
                    {
                        var controller = coll.EnsureController<VaporController>();
                        controller.Stop();
                    }
                }
            }
        }
    }
}
