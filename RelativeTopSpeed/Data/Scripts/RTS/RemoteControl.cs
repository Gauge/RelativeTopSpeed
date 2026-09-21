using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace RelativeTopSpeed
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), false)]
    public class RemoteControl : MyGameLogicComponent
    {
        private IMyTerminalControlSlider slider;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyRemoteControl>(out controls);
            foreach (var control in controls)
                if (control.Id == "SpeedLimit") { slider = control as IMyTerminalControlSlider; break; }
            RelativeTopSpeed.SettingsChanged -= OnSettingsChanged;
            RelativeTopSpeed.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged(Settings.Instance);
        }

        public override void OnBeforeRemovedFromContainer()
        {
            RelativeTopSpeed.SettingsChanged -= OnSettingsChanged;
            slider = null;
        }

        private void OnSettingsChanged(Settings settings)
        {
            var remote = Entity as IMyRemoteControl;
            if (settings == null || remote == null) return;
            remote.SpeedLimit = settings.RemoteControlSpeedLimit;
            slider?.SetLimits(0, settings.RemoteControlSpeedLimit);
        }
    }
}
