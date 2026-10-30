using System;
using UnityEngine;

// Maps avatar species ids (used in config.json) and gender to avatar prefabs.
// Species that exist in one variant only use the same prefab for both genders.
[CreateAssetMenu(menuName = "Mirror Game/Avatar Database")]
public class AvatarDatabase : ScriptableObject
{
    [Serializable]
    public class Species
    {
        public string id;
        public AvatarBody male;
        public AvatarBody female;
    }

    [SerializeField] Species[] species;

    public string[] Ids => Array.ConvertAll(species, s => s.id);

    // Unknown ids fall back to the first species, so a typo in the config never breaks the game.
    public AvatarBody Get(string id, string gender)
    {
        var entry = Array.Find(species, s => s.id == id);
        if (entry == null)
        {
            Debug.LogWarning($"Unknown avatar species '{id}', using '{species[0].id}'.");
            entry = species[0];
        }
        return gender == "w" ? entry.female : entry.male;
    }
}
