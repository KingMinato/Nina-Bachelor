using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Test tool (starts inactive, GameFlow activates it in the Unity Editor and in test runs):
// switches the player avatar instantly. The "Avatar" row opens a panel with the study avatars (s = 1-4),
// the other candidates, gender and jump. The buttons are created from an inactive template button at startup.
public class DebugAvatarSwitcher : MonoBehaviour
{
    [SerializeField] AvatarSpawner spawner;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Button toggleButton;
    [SerializeField] GameObject panel;
    [SerializeField] Transform grid;
    [SerializeField] Button template;

    // Species ids of the conditions s = 1-4 (config.species), set by GameFlow before activating.
    [System.NonSerialized] public string[] study = new string[0];

    Button[] speciesButtons;
    string[] ids;
    Button maleButton;
    Button femaleButton;

    void Start()
    {
        ids = study.Concat(spawner.SpeciesIds.Except(study)).ToArray();
        speciesButtons = new Button[ids.Length];
        var studyGrid = DebugPanels.Section(grid, template, "Studie (s = 1–4)");
        var otherGrid = DebugPanels.Section(grid, template, "Weitere Kandidaten");
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            bool inStudy = i < study.Length;
            speciesButtons[i] = DebugPanels.AddButton(inStudy ? studyGrid : otherGrid, template,
                inStudy ? $"{i + 1} · {id}" : id, () => Switch(id, spawner.Gender));
        }
        var genderGrid = DebugPanels.Section(grid, template, "Geschlecht");
        maleButton = DebugPanels.AddButton(genderGrid, template, "männlich", () => Switch(spawner.SpeciesId, "m"));
        femaleButton = DebugPanels.AddButton(genderGrid, template, "weiblich", () => Switch(spawner.SpeciesId, "w"));
        DebugPanels.AddButton(DebugPanels.Section(grid, template, "Aktion"), template, "Springen", playerActions.Jump);

        toggleButton.onClick.AddListener(() => DebugPanels.Toggle(panel, toggleButton));
        panel.SetActive(false);
        DebugPanels.Mark(toggleButton, false);
        Highlight();
    }

    void Switch(string speciesId, string gender)
    {
        spawner.Spawn(speciesId, gender);
        Highlight();
    }

    void Highlight()
    {
        DebugPanels.SetRow(toggleButton, "Avatar", $"{spawner.SpeciesId} · {spawner.Gender}");
        for (int i = 0; i < ids.Length; i++)
            DebugPanels.Mark(speciesButtons[i], ids[i] == spawner.SpeciesId);
        DebugPanels.Mark(maleButton, spawner.Gender == "m");
        DebugPanels.Mark(femaleButton, spawner.Gender == "w");
    }
}
