using System;
using System.Collections.Generic;
using UnityEngine;

namespace TripeaksSolitaire.Core
{
    [Serializable]
    public class CardData
    {
        public string id;
        public string type; // "value", "lock", "key", "zap"
        public int depth;
        public float x;
        public float y;
        public int angle;
        public bool faceUp;
        public bool random;
        public int value; // Only used if random = false
        public int sequence;
        public List<ModifierData> modifiers;
    }

    [Serializable]
    public class ModifierData
    {
        public string type; // "bomb", etc.
        public BombProperties properties;
    }

    [Serializable]
    public class BombProperties
    {
        public int plays;
        public int timer;
    }

    [Serializable]
    public class LevelSettings
    {
        public int level_number;
        public string background;
        public List<int> cards_in_stack;
        public int star_1;
        public int star_2;
        public int star_3;
        public List<WinCriteria> win_criteria;
        public List<string> tags;
    }

    [Serializable]
    public class WinCriteria
    {
        public string type; // "clear_all"
    }

    [Serializable]
    public class LevelData
    {
        public string id;
        public string version;
        public List<CardData> cards;
        public LevelSettings settings;
    }
}
