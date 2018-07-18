﻿using System;
using System.Collections;
using System.Collections.Generic;
using ModCommon;
using Modding;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Logger = Modding.Logger;

namespace LostLord
{
    internal class KinFinder : MonoBehaviour
    {
        private GameObject _kin;
        
        private void Start()
        {
            Logger.Log("[Lost Lord] Added KinFinder MonoBehaviour");
        }

        private void Update()
        {
            if (_kin != null) return;
            _kin = GameObject.Find("Lost Kin");
            if (_kin == null) return;
            _kin.AddComponent<Kin>();
        }
    }
}