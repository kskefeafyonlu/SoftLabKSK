using _Project.Blacksmithing.Foundry;
using UnityEngine;
using UnityEngine.UI;

public class FoundryUIController : MonoBehaviour
{
    public Button alloyButton;
    public AlloyPopupMB alloyPopup;

    void Awake()
    {
        alloyButton.onClick.AddListener(() =>
        {
            alloyPopup.Open();
        });
    }
}