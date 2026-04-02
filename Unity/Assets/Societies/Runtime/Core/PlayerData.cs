using System;
using System.Collections.Generic;
using UnityEngine;

namespace Societies.Runtime.Core
{
    [Serializable]
    public class PlayerData
    {
        public string PlayerId;
        public string PlayerName;
        public Vector3 Position;
        public int Credits;
        public List<int> KnownRecipes = new();
        public float PlayTime;

        public PlayerData(string name)
        {
            PlayerId = Guid.NewGuid().ToString();
            PlayerName = name;
            Position = Vector3.zero;
            Credits = 100;
            PlayTime = 0;
        }
    }
}
