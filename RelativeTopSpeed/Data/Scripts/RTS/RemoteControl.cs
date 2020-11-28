using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace RelativeTopSpeed
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), false)]
	public class RemoteControl : MyGameLogicComponent
	{

		

		private static bool AreControlsInitialized;
        private IMyTerminalControlSlider SliderControl;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

			List<IMyTerminalControl> controls;
			MyAPIGateway.TerminalControls.GetControls<IMyRemoteControl>(out controls);

			IMyTerminalControl control = controls.FirstOrDefault(c => c.Id == "SpeedLimit");
			SliderControl = control as IMyTerminalControlSlider;
			RelativeTopSpeed.SettingsChanged += OnSettingsChanged;
			OnSettingsChanged(Settings.Instance);
		}

		public override void OnBeforeRemovedFromContainer()
		{
			RelativeTopSpeed.SettingsChanged -= OnSettingsChanged;
		}

		private void OnSettingsChanged(Settings s) 
		{
			if (SliderControl != null && s != null)
			{
				((IMyRemoteControl)Entity).SpeedLimit = s.RemoteControlSpeedLimit;
				SliderControl?.SetLimits(0, s.RemoteControlSpeedLimit);
			}
		}
	}
}
