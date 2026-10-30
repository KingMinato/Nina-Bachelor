using UnityEngine;

// Keeps only one debug switcher panel open: opening one closes the other.
public static class DebugPanels
{
    static GameObject open;

    public static void Toggle(GameObject panel)
    {
        bool show = !panel.activeSelf;
        if (show && open != null && open != panel)
            open.SetActive(false);
        panel.SetActive(show);
        open = show ? panel : null;
    }
}
