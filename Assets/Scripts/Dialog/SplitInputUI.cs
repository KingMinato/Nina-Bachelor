using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Split input for rounds 1 and 3: slider plus -/+ buttons in splitStep steps,
// live shares label and a confirm button. Starts inactive in the scene.
public class SplitInputUI : MonoBehaviour
{
    [SerializeField] TMP_Text messageText;
    [SerializeField] Slider slider;
    [SerializeField] Button minusButton;
    [SerializeField] Button plusButton;
    [SerializeField] TMP_Text sharesText;
    [SerializeField] Button confirmButton;
    [SerializeField] TMP_Text confirmLabel;

    [Tooltip("Controls stay disabled this long after the panel appears, so a double tap cannot confirm by accident.")]
    [SerializeField] float inputDelaySeconds = 0.4f;

    Config config;

    // Runs on first activation. The slider works in whole step units, so it can only land on valid amounts.
    void Awake()
    {
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(_ => UpdateShares());
        minusButton.onClick.AddListener(() => slider.value -= 1);
        plusButton.onClick.AddListener(() => slider.value += 1);
    }

    int Step => Mathf.Max(1, config.splitStep);
    int PlayerKeeps => UltimatumRules.SnapToStep((int)slider.value * Step, config);

    // Shows the panel and completes with the amount the player keeps once confirmed.
    public async Awaitable<int> Ask(string message, Config config, int startPlayerKeeps)
    {
        this.config = config;
        messageText.text = message;
        confirmLabel.text = config.texts.confirm;
        slider.minValue = 0;
        slider.maxValue = config.moneyPerRound / Step;
        slider.SetValueWithoutNotify(UltimatumRules.SnapToStep(startPlayerKeeps, config) / Step);
        UpdateShares();

        SetInteractable(false);
        gameObject.SetActive(true);
        await Awaitable.WaitForSecondsAsync(inputDelaySeconds);
        SetInteractable(true);

        var confirmed = new AwaitableCompletionSource();
        void OnConfirm() => confirmed.TrySetResult();
        confirmButton.onClick.AddListener(OnConfirm);
        await confirmed.Awaitable;
        confirmButton.onClick.RemoveListener(OnConfirm);

        SetInteractable(false);
        gameObject.SetActive(false);
        return PlayerKeeps;
    }

    void UpdateShares()
    {
        int keeps = PlayerKeeps;
        sharesText.text = Texts.Fill(config.texts.splitShares, keeps, config.moneyPerRound - keeps);
    }

    void SetInteractable(bool value)
    {
        slider.interactable = value;
        minusButton.interactable = value;
        plusButton.interactable = value;
        confirmButton.interactable = value;
    }
}
