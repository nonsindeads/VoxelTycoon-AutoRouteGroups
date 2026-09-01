using System;
using VoxelTycoon.Game.UI.ModernUI;

namespace AutoRouteGroups
{
    internal sealed class InstantToolbarAction : ToolbarAction
    {
        private readonly Action _action;

        public InstantToolbarAction(Action action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool IsActive => false;

        public override void Toggle()
        {
            _action();
        }
    }
}
