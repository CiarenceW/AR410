using Receiver2;
using Receiver2ModdingKit;
using Receiver2ModdingKit.CustomSounds;
using RewiredConsts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AR410
{
	public class AR410 : ModGunScript
	{
		private float slide_forward_speed = -50f;
		private float hammer_accel = -8000;
		private float m_charging_handle_amount;
		private readonly float[] slide_push_hammer_curve = new float[] {
			0f,
			0f,
			0.25f,
			0.8f,
			0.5f,
			0.95f
		};

		public override void AwakeGun()
		{
			hammer.amount = 1;
		}

		public override void OnHolster()
		{
			ModAudioManager.PlayOneShotAttached("event:/AR410/AR410_holster", this.transform);
		}

		public override void OnUnholster()
		{
			ModAudioManager.PlayOneShotAttached("event:/AR410/AR410_unholster", this.transform);
		}

		private void FireBulletShotgun(ShellCasingScript round)
		{
			chamber_check_performed = false;

			CartridgeSpec cartridge_spec = default;
			cartridge_spec.SetFromPreset(round.cartridge_type);
			LocalAimHandler holdingPlayer = GetHoldingPlayer();

			Vector3 direction = transform_bullet_fire.rotation * Vector3.forward;
			BulletTrajectory bulletTrajectory = BulletTrajectoryManager.PlanTrajectory(transform_bullet_fire.position, cartridge_spec, direction, right_hand_twist);

			if (ConfigFiles.global.display_trajectory_window && ConfigFiles.global.display_trajectory_window_show_debug)
			{
				bulletTrajectory.draw_path = BulletTrajectory.DrawType.Debug;
			}
			else if (round.tracer || GunScript.force_tracers)
			{
				bulletTrajectory.draw_path = BulletTrajectory.DrawType.Tracer;
				bulletTrajectory.tracer_fuse = true;
			}

			if (holdingPlayer != null)
			{
				bulletTrajectory.bullet_source = gameObject;
				bulletTrajectory.bullet_source_entity_type = ReceiverEntityType.Player;
			}
			else
			{
				bulletTrajectory.bullet_source = gameObject;
				bulletTrajectory.bullet_source_entity_type = ReceiverEntityType.UnheldGun;
			}
			BulletTrajectoryManager.ExecuteTrajectory(bulletTrajectory);

			rotation_transfer_y += UnityEngine.Random.Range(rotation_transfer_y_min, rotation_transfer_y_max);
			rotation_transfer_x += UnityEngine.Random.Range(rotation_transfer_x_min, rotation_transfer_x_max);
			recoil_transfer_x -= UnityEngine.Random.Range(recoil_transfer_x_min, recoil_transfer_x_max);
			recoil_transfer_y += UnityEngine.Random.Range(recoil_transfer_y_min, recoil_transfer_y_max);
			add_head_recoil = true;

			if (CanMalfunction && malfunction == GunScript.Malfunction.None && (UnityEngine.Random.Range(0f, 1f) < doubleFeedProbability || force_double_feed_failure))
			{
				if (force_double_feed_failure && force_just_one_failure)
				{
					force_double_feed_failure = false;
				}
				malfunction = GunScript.Malfunction.DoubleFeed;
				ReceiverEvents.TriggerEvent(ReceiverEventTypeInt.GunMalfunctioned, 2);
			}

			ReceiverEvents.TriggerEvent(ReceiverEventTypeVoid.PlayerShotFired);

			last_time_fired = Time.time;
			last_frame_fired = Time.frameCount;
			dry_fired = false;

			if (shots_until_dirty > 0)
			{
				shots_until_dirty--;
			}

			yoke_stage = YokeStage.Closed;
		}
		private void TryFireBulletShotgun()
		{
			Vector3 originalRotation = transform.Find("point_bullet_fire").localEulerAngles;

			transform_bullet_fire.localEulerAngles += new Vector3(
				UnityEngine.Random.Range(-0.2f, 0.2f),
				UnityEngine.Random.Range(-0.2f, 0.2f),
				0
			);

			TryFireBullet(1);

			if (dry_fired) return;

			transform_bullet_fire.localEulerAngles = originalRotation;

			for (int i = 0; i < 7; i++)
			{
				float angle = UnityEngine.Random.Range(0f, (float)System.Math.PI * 1.2f);
				float diversion = UnityEngine.Random.Range(0f, 1.2f);

				float moveX = Mathf.Sin(angle) * diversion;
				float moveY = Mathf.Cos(angle) * diversion;

				transform_bullet_fire.localEulerAngles += new Vector3(
					moveX,
					moveY,
					0
				);

				FireBulletShotgun(round_in_chamber);

				transform_bullet_fire.localEulerAngles = originalRotation;
			}
		}

		public override void UpdateGun()
		{
			hammer.asleep = true;
			hammer.accel = hammer_accel;

			if (slide.amount > 0)
			{ // Bolt cocks the hammer when moving back 
				hammer.amount = Mathf.Max(hammer.amount, InterpCurve(slide_push_hammer_curve, slide.amount));

				hammer.UpdateDisplay();
			}

			if (hammer.amount > _hammer_cocked_val) _hammer_state = 3;

			if (IsSafetyOn())
			{
				trigger.amount = Mathf.Min(trigger.amount, 0.1f);

				trigger.UpdateDisplay();
			};

			if (slide.amount == 0 && _hammer_state == 3 && trigger.amount == 1)
			{ // Simulate auto sear
				hammer.amount = Mathf.MoveTowards(hammer.amount, _hammer_cocked_val, Time.deltaTime * Time.timeScale * 50);
				if (hammer.amount == _hammer_cocked_val) _hammer_state = 2;
			}

			if (hammer.amount == 0 && _hammer_state == 2)
			{ // If hammer dropped and hammer was cocked then fire gun and decock hammer
				TryFireBulletShotgun();

				_hammer_state = 0;
			}

			if (slide.vel < 0) slide.vel = Mathf.Max(slide.vel, slide_forward_speed); // Slow down the slide moving forward, reducing fire rate

			if (slide_stop.amount == 1)
			{
				slide_stop.asleep = true;
			}

			if (slide.amount == 0 && _hammer_state == 3)
			{
				hammer.amount = Mathf.MoveTowards(hammer.amount, _hammer_cocked_val, Time.deltaTime * Time.timeScale * 50);
				if (hammer.amount == _hammer_cocked_val) _hammer_state = 2;
			}

			if (_hammer_state != 3 && ((trigger.amount >= 0.5f && slide.amount == 0) || hammer.amount != _hammer_cocked_val))
			{
				hammer.asleep = false;
			}

			hammer.TimeStep(Time.deltaTime);

			if (player_input.GetButton(Action.Pull_Back_Slide) || player_input.GetButtonUp(Action.Pull_Back_Slide))
			{
				m_charging_handle_amount = Mathf.MoveTowards(m_charging_handle_amount, slide.amount, Time.deltaTime * 20f / Time.timeScale);
			}
			else
			{
				m_charging_handle_amount = Mathf.MoveTowards(m_charging_handle_amount, 0, Time.deltaTime * 50f);
			}

			ApplyTransform("charging_handle", m_charging_handle_amount, transform.Find("charging_handle"));

			ApplyTransform("charging_handle_latch", m_charging_handle_amount, transform.Find("charging_handle/charging_handle_latch"));

			var chargingHandlePullRot = new Vector3();

			var chargingHandlePullPos = new Vector3();

			ApplyTransform("charging_handle_pull", m_charging_handle_amount, ref chargingHandlePullPos, ref chargingHandlePullRot);

			transform.Find("charging_handle/charging_handle_pull_left").localEulerAngles = chargingHandlePullRot;

			transform.Find("charging_handle/charging_handle_pull_right").localEulerAngles = -chargingHandlePullRot;

			hammer.UpdateDisplay();

			slide_stop.UpdateDisplay();
		}
	}
}
