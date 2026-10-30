using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Test tool (starts inactive, GameFlow activates it in the Unity Editor and in test runs):
// switches the player avatar instantly. The "Avatar" button opens a grid with all species, gender, jump and close.
// The grid buttons are created from an inactive template button at startup.
public class DebugAvatarSwitcher : MonoBehaviour
{
    static readonly Color Normal = Color.white;
    static readonly Color Selected = new Color(1f, 0.85f, 0.4f);

    [SerializeField] AvatarSpawner spawner;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] DebugOverlay overlay;
    [SerializeField] Button toggleButton;
    [SerializeField] GameObject panel;
    [SerializeField] Transform grid;
    [SerializeField] Button template;

    Button[] speciesButtons;
    Button maleButton;
    Button femaleButton;

    void Start()
    {
        var ids = spawner.SpeciesIds;
        speciesButtons = new Button[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            speciesButtons[i] = AddButton(id, () => Switch(id, spawner.Gender));
        }
        maleButton = AddButton("männlich", () => Switch(spawner.SpeciesId, "m"));
        femaleButton = AddButton("weiblich", () => Switch(spawner.SpeciesId, "w"));
        AddButton("Springen", playerActions.Jump);
        AddButton("Schließen", () => panel.SetActive(false));

        toggleButton.onClick.AddListener(() =>
        {
            panel.SetActive(!panel.activeSelf);
            Highlight();
        });
        panel.SetActive(false);
    }

    Button AddButton(string label, UnityAction onClick)
    {
        var button = Instantiate(template, grid);
        button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.onClick.AddListener(onClick);
        return button;
    }

    void Switch(string speciesId, string gender)
    {
        spawner.Spawn(speciesId, gender);
        overlay.ShowAvatar(speciesId, gender);
        Highlight();
    }

    void Highlight()
    {
        var ids = spawner.SpeciesIds;
        for (int i = 0; i < ids.Length; i++)
            speciesButtons[i].image.color = ids[i] == spawner.SpeciesId ? Selected : Normal;
        maleButton.image.color = spawner.Gender == "m" ? Selected : Normal;
        femaleButton.image.color = spawner.Gender == "w" ? Selected : Normal;
    }
}
