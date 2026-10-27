using TMPro;
using UnityEngine;

// Balance label, visible from the start of the dialog until the redirect.
// Starts inactive in the scene, UltimatumController activates it.
public class BalanceUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    string caption = "";

    public void Show(string caption, int balance)
    {
        this.caption = caption;
        Set(balance);
        gameObject.SetActive(true);
    }

    public void Set(int balance)
    {
        label.text = $"{caption}: {balance} CHF";
    }
}
