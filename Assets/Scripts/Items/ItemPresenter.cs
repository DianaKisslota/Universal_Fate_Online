using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class ItemPresenter : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _nameText;

    private Transform _transportPanel;
    private Transform _oldParent;
    protected CanvasGroup _canvasGroup;
    protected RectTransform _rectTransform;
    private StoragePosition _storagePosition;
    public StoragePosition StoragePosition 
    { 
        get => _storagePosition;
        set
        {
            _storagePosition = value;
            RefreshInfo();
        } 
    }

    public Item Item => StoragePosition.Item;

    public string Name => Item.Name;
    public Transform OldParent => _oldParent;

    public void HideName()
    {
        _nameText.gameObject.SetActive(false);
    }
    public Sprite Icon 
    { set {_icon.sprite = value;}}

    public int Count
    {
        get => StoragePosition.Count;
        set
        {
            StoragePosition.Count = value;
            if (StoragePosition.Count == 0 && gameObject != null)
                Destroy(gameObject);
            else
                RefreshInfo();
        }
    }

    public void RefreshInfo()
    {
        if (Count > 1)
            _countText.text = Count.ToString();
        else
           _countText.text = string.Empty;
        if (Item is RangeWeapon rangeWeapon)
        {
            _countText.text = rangeWeapon.AmmoCount.ToString() + "/" + rangeWeapon.AmmoCapacity.ToString();
        }
    }
    private void Start()
    {
        _transportPanel = GameObject.Find("TransportPanel").transform;
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _nameText.text = Name;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = false;
        _oldParent = transform.parent;
        transform.SetParent(_transportPanel);
        transform.localEulerAngles = Vector3.zero;
        if (_oldParent.TryGetComponent<DropSlot>(out var dropSlot))
            dropSlot.OnItemLeave(Item);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _rectTransform.anchoredPosition += eventData.delta;// / _mainCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (transform.parent == _transportPanel)
        {
            transform.SetParent(_oldParent);
            transform.localPosition = Vector2.zero;
            if (transform.parent.gameObject.TryGetComponent<StorageSlot>(out var storageSlot))
            {
                storageSlot.InsertItem(this);
            }
        }

        if (transform.parent.gameObject.TryGetComponent<ItemSlot>(out var itemSlot))
        {
            if (itemSlot.SlotType == SlotType.Shoulder)
                eventData.pointerDrag.transform.localEulerAngles = new Vector3(0, 0, -90);
            _nameText.gameObject.SetActive(false);
        }
        else
            _nameText.gameObject.SetActive(true);

        _canvasGroup.blocksRaycasts = true;
    }
}
