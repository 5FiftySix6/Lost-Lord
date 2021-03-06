﻿using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using ModCommon.Util;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Modding.Logger;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace LostLord
{
    internal class Kin : MonoBehaviour
    {
        private readonly Dictionary<string, float> _fpsDict = new Dictionary<string, float>
        {
            ["Dash Antic 1"] = 30,
            ["Dash Antic 2"] = 30,
            ["Dash Antic 3"] = 30,
            ["Dash Attack 1"] = 60,
            ["Dash Attack 2"] = 90,
            ["Dash Attack 3"] = 50,
            ["Jump Antic"] = 30,
            ["Jump"] = 60,
            ["Downstab"] = 100,
            ["Downstab Antic"] = 70,
            ["Downstab Land"] = 30,
            ["Downstab Slam"] = 30,
            ["Land"] = 60,
            ["Overhead Slash"] = 20,
            ["Overhead Slashing"] = 20,
            ["Overhead Antic"] = 34,
            ["Roar Start"] = 20,
            ["Roar Loop"] = 20,
            ["Roar End"] = 20
        };

        private HealthManager _hm;

        private tk2dSpriteAnimator _anim;

        private float[] _origFps;

        private Recoil _recoil;

        private InfectedEnemyEffects _enemyEffects;

        private PlayMakerFSM _stunControl;
        private PlayMakerFSM _balloons;
        private PlayMakerFSM _control;

        private static bool _changedKin;

        private static Sprite _headGlob;

        private Texture _oldTex;

        private void Awake()
        {
            Log("Added Kin MonoBehaviour");

            if (!PlayerData.instance.infectedKnightDreamDefeated) return;
            if (!LostLord.Instance.IsInHall) return;

            ModHooks.Instance.ObjectPoolSpawnHook += Projectile;
            On.EnemyDeathEffects.EmitInfectedEffects += OnEmitInfected;
            On.EnemyDeathEffects.EmitEffects += No;
            On.EnemyDeathEffects.EmitCorpse += EmitCorpse;
            On.InfectedEnemyEffects.RecieveHitEffect += RecieveHit;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnDestroy2;

            _hm = gameObject.GetComponent<HealthManager>();
            _stunControl = gameObject.LocateMyFSM("Stun Control");
            _balloons = gameObject.LocateMyFSM("Spawn Balloon");
            _anim = gameObject.GetComponent<tk2dSpriteAnimator>();
            _control = gameObject.LocateMyFSM("IK Control");
            _recoil = gameObject.GetComponent<Recoil>();
            _enemyEffects = gameObject.GetComponent<InfectedEnemyEffects>();
        }

        private void OnDestroy2(Scene arg0, Scene arg1) => OnDestroy();

        private void RecieveHit(On.InfectedEnemyEffects.orig_RecieveHitEffect orig, InfectedEnemyEffects self, float attackdirection)
        {
            if (self.GetAttr<bool>("didFireThisFrame"))
            {
                return;
            }

            if (self.GetAttr<SpriteFlash>("spriteFlash") != null)
            {
                self.GetAttr<SpriteFlash>("spriteFlash").flashShadeGet();
            }

            FSMUtility.SendEventToGameObject(gameObject, "DAMAGE FLASH", true);
            self.GetAttr<AudioEvent>("impactAudio").SpawnAndPlayOneShot(self.GetAttr<AudioSource>("audioSourcePrefab"), self.transform.position);
            self.SetAttr("didFireThisFrame", true);
        }

        private static void EmitCorpse(On.EnemyDeathEffects.orig_EmitCorpse orig, EnemyDeathEffects self, float? attackdirection, bool iswatery, bool spellburn)
        {
            orig(self, attackdirection, true, true);
        }

        private static void No(On.EnemyDeathEffects.orig_EmitEffects orig, EnemyDeathEffects self)
        {
            // no
        }


        private static void OnEmitInfected(On.EnemyDeathEffects.orig_EmitInfectedEffects orig, EnemyDeathEffects self)
        {
            self.EmitSound();

            if (self.GetAttr<GameObject>("corpse") != null)
            {
                var component = self.GetAttr<GameObject>("corpse").GetComponent<SpriteFlash>();

                if (component != null)
                {
                    component.FlashShadowRecharge();
                    component.flashShadeGet();
                }
            }

            GameCameras.instance.cameraShakeFSM.SendEvent("EnemyKillShake");
        }

        private void Start()
        {
            if (!PlayerData.instance.infectedKnightDreamDefeated) return;
            if (!LostLord.Instance.IsInHall) return;

            Log(_changedKin);

            if (!_changedKin)
            {
                tk2dSpriteDefinition def = gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef();

                _oldTex = def.material.mainTexture;

                def.material.mainTexture = LostLord.SPRITES[0].texture;

                _changedKin = true;
            }

            _origFps = _origFps ?? _anim.Library.clips.Select(x => x.fps).ToArray();

            PlayMakerFSM corpse = gameObject.FindGameObjectInChildren("Corpse Infected Knight Dream(Clone)").LocateMyFSM("corpse");

            corpse.RemoveAction("Init", 9);
            corpse.RemoveAction("Init", 1);
            corpse.ChangeTransition("Pause", "FINISHED", "BG Open");

            corpse.AddAction("Pause", corpse.GetAction<Tk2dPlayAnimation>("Blow", 9));

            // Refill MP
            HeroController.instance.AddMPChargeSpa(999);

            // No stunning
            Destroy(_stunControl);

            // 🅱lood
            _enemyEffects.SetAttr("noBlood", true);

            // No balloons
            _balloons.ChangeTransition("Spawn Pause", "SPAWN", "Stop");


            // 1500hp
            _hm.hp = 1500;

            // Disable Knockback
            _recoil.enabled = false;

            // 2x Damage on All Components
            foreach (DamageHero i in gameObject.GetComponentsInChildren<DamageHero>(true))
            {
                Log(i.name);
                i.damageDealt *= 2;
            }

            // Speed up some attacks.
            foreach (KeyValuePair<string, float> i in _fpsDict)
            {
                _anim.GetClipByName(i.Key).fps = i.Value;
            }

            // Decrease idles
            _control.GetAction<WaitRandom>("Idle", 5).timeMax = 0.01f;
            _control.GetAction<WaitRandom>("Idle", 5).timeMin = 0.001f;

            // 2x Damage
            _control.GetAction<SetDamageHeroAmount>("Roar End", 3).damageDealt.Value = 2;

            // Increase Jump X
            _control.GetAction<FloatMultiply>("Aim Dstab", 3).multiplyBy = 5;
            _control.GetAction<FloatMultiply>("Aim Jump", 3).multiplyBy = 2.2f;

            // Decrease walk idles.
            var walk = _control.GetAction<RandomFloat>("Idle", 3);
            walk.min = 0.001f;
            walk.max = 0.01f;

            // Speed up
            _control.GetAction<Wait>("Jump", 5).time = 0.01f;
            _control.GetAction<Wait>("Dash Antic 2", 2).time = 0.27f;

            // Fall faster.
            _control.GetAction<SetVelocity2d>("Dstab Fall", 4).y = -200; // -130; // -90
            _control.GetAction<SetVelocity2d>("Dstab Fall", 4).everyFrame = true;

            _control.GetAction<ActivateGameObject>("Dstab Land", 2).activate = false;
            _control.GetAction<ActivateGameObject>("Dstab Fall", 6).activate = false;

            // Combo Dash into Upslash followed by Dstab's Projectiles.
            _control.CopyState("Dstab Land", "Spawners");
            _control.CopyState("Ohead Slashing", "Ohead Combo");
            _control.CopyState("Dstab Recover", "Dstab Recover 2");

            _control.ChangeTransition("Dash Recover", "FINISHED", "Ohead Combo");

            _control.RemoveAnim("Dash Recover", 3);
            _control.RemoveAnim("Spawners", 3);

            _control.ChangeTransition("Ohead Combo", "FINISHED", "Spawners");
            _control.ChangeTransition("Spawners", "FINISHED", "Dstab Recover 2");
            _control.GetAction<Wait>("Dstab Recover 2", 0).time = 0f;

            List<FsmStateAction> a = _control.GetState("Dstab Fall").Actions.ToList();
            a.AddRange(_control.GetState("Spawners").Actions);

            _control.GetState("Dstab Fall").Actions = a.ToArray();

            // Spawners before Overhead Slashing.
            _control.CopyState("Spawners", "Spawn Ohead");
            _control.ChangeTransition("Ohead Antic", "FINISHED", "Spawn Ohead");
            _control.ChangeTransition("Spawn Ohead", "FINISHED", "Ohead Slashing");
            _control.FsmVariables.GetFsmFloat("Evade Range").Value *= 2;

            // Dstab => Upslash
            _control.CopyState("Ohead Slashing", "Ohead Combo 2");
            _control.ChangeTransition("Dstab Land", "FINISHED", "Ohead Combo 2");
            _control.ChangeTransition("Ohead Combo 2", "FINISHED", "Dstab Recover");

            // Aerial Dash => Dstab
            _control.ChangeTransition("Dash Recover", "FALL", "Dstab Antic");

            // bingo bongo ur dash is now lightspeed
            _control.FsmVariables.GetFsmFloat("Dash Speed").Value *= 2;
            _control.FsmVariables.GetFsmFloat("Dash Reverse").Value *= 2;

            // Fixes the cheese where you can sit on the wall
            // right above where he can jump and then just spam ddark
            _control.CopyState("Jump", "Cheese Jump");
            _control.GetAction<Wait>("Cheese Jump", 5).time.Value *= 5;
            _control.RemoveAction("Cheese Jump", 4);
            _control.InsertAction("Cheese Jump", new FireAtTarget
            {
                gameObject = new FsmOwnerDefault {GameObject = gameObject},
                target = HeroController.instance.gameObject,
                speed = 100f,
                everyFrame = false,
                spread = 0f,
                position = new Vector3(0, 0)
            }, 4);

            foreach (string i in new[] {"Damage Response", "Attack Choice"})
            {
                _control.InsertMethod(i, 0, StopCheese);
            }

            Log("fin.");
        }

        [UsedImplicitly]
        public void StopCheese()
        {
            float hx = HeroController.instance.gameObject.transform.GetPositionX();
            float hy = HeroController.instance.gameObject.transform.GetPositionY();

            if (hy > 35 && (15 < hx && hx < 16.6 || 36.55 < hx && hx < 37.8))
            {
                _control.SetState("Cheese Jump");
            }
        }

        private GameObject Projectile(GameObject go)
        {
            if (go.name != "IK Projectile DS(Clone)") return go;

            if (this == null)
            {
                var sre = go.GetComponentInChildren<SpriteRenderer>(true);

                if (!string.IsNullOrEmpty(sre.sprite.name))
                {
                    ModHooks.Instance.ObjectPoolSpawnHook -= Projectile;
                }

                // Broken Vessel Fix
                RevertProjectile(go);

                return go;
            }

            foreach (DamageHero i in go.GetComponentsInChildren<DamageHero>(true))
            {
                i.damageDealt = 2;
            }

            var psr = go.GetComponentInChildren<ParticleSystemRenderer>();

            var m = new Material(psr.material) {color = Color.black};

            psr.material = m;

            var sr = go.GetComponentInChildren<SpriteRenderer>(true);

            // ReSharper disable once Unity.NoNullCoalescing
            _headGlob = _headGlob ?? sr.sprite;

            sr.sprite = LostLord.SPRITES[1];

            return go;
        }

        private static void RevertProjectile(GameObject go)
        {
            var psr = go.GetComponentInChildren<ParticleSystemRenderer>();

            psr.material.color = Color.white;

            var sr = go.GetComponentInChildren<SpriteRenderer>(true);

            sr.sprite = _headGlob;
        }

        private void OnDestroy()
        {
            On.EnemyDeathEffects.EmitInfectedEffects -= OnEmitInfected;
            On.EnemyDeathEffects.EmitEffects -= No;
            On.EnemyDeathEffects.EmitCorpse -= EmitCorpse;
            On.InfectedEnemyEffects.RecieveHitEffect -= RecieveHit;

            if (_origFps == null) return;

            for (int i = 0; i < _origFps.Length; i++)
            {
                _anim.Library.clips[i].fps = _origFps[i];
            }

            if (!_changedKin) return;

            tk2dSpriteDefinition def = gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef();

            def.material.mainTexture = _oldTex;

            _changedKin = false;
        }

        private static void Log(object obj)
        {
            Logger.Log("[Lost Lord] " + obj);
        }
    }
}