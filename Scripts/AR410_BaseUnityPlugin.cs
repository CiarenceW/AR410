using BepInEx;
using Receiver2ModdingKit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AR410
{
	[BepInPlugin("CiarenceW.AR410", "AR410", "1.0.0")]
	public class AR410_BaseUnityPlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			var bepInAttribute = this.GetBepInAttribute();

			Logger.LogDebugWithColor($"Plugin {bepInAttribute.GUID} version {bepInAttribute.Version} ", System.ConsoleColor.Green);
		}
	}
}
