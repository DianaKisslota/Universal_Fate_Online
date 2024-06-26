using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterController : AvatarController
{
    [SerializeField] private Material AccessMoveMaterial;
    [SerializeField] private Material RestrictMoveMaterial;
    [SerializeField] private CharacterInventoryPresenter _inventoryPanel;
    [SerializeField] private ContainerPresenter _containerPresenter;
    [SerializeField] private Transform _originInventoryPlaceHolder;
    [SerializeField] private Transform _containerInventoryPlaceHolder;
    [SerializeField] private GameObject _FinishBattleButton;
    [SerializeField] private Toggle _SetFireMode1;
    [SerializeField] private Toggle _SetFireMode2;
    [SerializeField] private Toggle _SetFireMode3;
    [SerializeField] private Image _mainWeaponImage;
    [SerializeField] private Image _mainWeaponAmmo;

    [SerializeField] EventTrigger _fireModeTrigger1;
    [SerializeField] EventTrigger _fireModeTrigger2;
    [SerializeField] EventTrigger _fireModeTrigger3;

    [SerializeField] private PointerController _pointer;
    //   [SerializeField] protected LineRenderer _pathDrawer;

    [SerializeField] private List<UIItem> _uiItems;

    private Vector3 _originPoint;
    private Vector3 _originAngle;

    private Vector3 _lastPoint;
    private Vector3 _lastAngle;

    private bool _mouseOverUI;

    private CharacterAvatar _playerAvatar => _avatar as CharacterAvatar;

    private List<GameObject> _navPoints = new List<GameObject>();
    private List<GameObject> _targets = new List<GameObject>();


    private bool _canMove;

    private bool _avatarMoving;
    private bool _avatarApplyingQants;
    private bool _quantsReverting;
    private bool AvatarBusy => _avatarMoving || _avatarApplyingQants;

    private void Start()
    {
        _originInventoryPlaceHolder = _inventoryPanel.transform.parent;
        _containerPresenter.gameObject.SetActive(false);
        _inventoryPanel.gameObject.SetActive(false);
        _playerAvatar.StartMoving += () => _avatarMoving = true;
        _playerAvatar.EndMoving += () => _avatarMoving = false;
        _playerAvatar.StartApplainQuants += () => _avatarApplyingQants = true;
        _playerAvatar.EndApplainQuants += () =>
        {
            _originPoint = _playerAvatar.transform.position;
            _originAngle = _playerAvatar.transform.eulerAngles;
            _avatarApplyingQants = false;
        };

        _originPoint = _playerAvatar.transform.position;
        _originAngle = _playerAvatar.transform.eulerAngles;
        _lastPoint = _playerAvatar.transform.position;
        _lastAngle = _playerAvatar.transform.eulerAngles;

        foreach(var item in _uiItems)
        {
            item.MouseOver += UIMouseInteract;
        }

        _inventoryPanel.Inventory.ItemLeave += ItemLeave;
        _inventoryPanel.Inventory.ItemPresenterSet += ItemPresenterSet;
        foreach (var itemSlot in _inventoryPanel.ItemSlots)
        {
            itemSlot.ItemLeave += ItemLeave;
            itemSlot.ItemPresenterSet += ItemPresenterSet;
        }

        _inventoryPanel.Inventory.WeaponReloaded += OnWeaponReloaded;
        _playerAvatar.FireModeSet += OnFireModeSet;
    }

    private void OnDestroy()
    {
        foreach (var itemSlot in _inventoryPanel.ItemSlots)
        {
            itemSlot.ItemLeave -= ItemLeave;
            itemSlot.ItemPresenterSet -= ItemPresenterSet;
        }
        _inventoryPanel.Inventory.WeaponReloaded -= OnWeaponReloaded;
        _playerAvatar.FireModeSet -= OnFireModeSet;
    }

    public override void BindAvatar(EntityAvatar avatar)
    {
        base.BindAvatar(avatar);
        _playerAvatar.ReflectAllItems();
        ReflectMainWeapon(_playerAvatar.Character.MainWeapon);
        _playerAvatar.InventoryPresenter = _inventoryPanel;
        _playerAvatar.ItemPresenterTransferred += ItemPresenterSet;
    }

    public void UIMouseInteract(bool mouseOverUI)
    {
        _mouseOverUI = mouseOverUI;
    }

    private void Update()
    {
        if (AvatarBusy)
            return;
        _FinishBattleButton.SetActive(!(_containerPresenter.gameObject.activeSelf ||
                                        (_inventoryPanel.gameObject.activeSelf && _inventoryPanel.transform.parent == _originInventoryPlaceHolder)));

        var entityAvatar = GetEntityAvatarUnderMousePoint();
        if (entityAvatar != null)
        {
            _pointer.SetPointerType(PointerType.Target);
            _pointer.SetActive(true);
            _pointer.position = entityAvatar.transform.position;

            if (Input.GetMouseButtonDown(0))
            {
                var target = Instantiate(Global.TargetPrefab);
                target.transform.position = _pointer.position;
                _targets.Add(target);
                AttackInfo attackInfo;
                if (_playerAvatar.Character.MainWeapon != null && _playerAvatar.Character.MainWeapon is RangeWeapon rangedWeapon)
                {
                    var ammoUsed = Mathf.Min(rangedWeapon.GetFireModeAmmo(_playerAvatar.FireMode).Value, rangedWeapon.AmmoCount);
                    rangedWeapon.UnLoad(ammoUsed);
                    _playerAvatar.InventoryPresenter.RefreshItemSlots();
                    attackInfo = new AttackInfo(_playerAvatar.FireMode, rangedWeapon.CurrentAmmoType, ammoUsed, entityAvatar);
                }
                else
                {
                    attackInfo = new AttackInfo(FireMode.Undefined, null, 0, entityAvatar);
                }
                _playerAvatar.AddAttackQuant(attackInfo);
                _playerAvatar.LookForShoot(entityAvatar);
            }

            return;
        }
        else
        {
            _pointer.SetPointerType(PointerType.Nav);
        }

        if (Input.GetMouseButtonDown(0))
        {
            var itemObject = GetItemUnderMousePoint();
            if (itemObject != null  && Vector3.Distance(_playerAvatar.transform.position, itemObject.transform.position) < 1.2f)                
            {
                _playerAvatar.AddPickObjectQuant(itemObject);
                _playerAvatar.TakeItem(itemObject);
                return;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            var containerObject = GetContainerUnderMousePoint();
            if (containerObject != null && Vector3.Distance(_playerAvatar.transform.position, containerObject.transform.position) < 1.5f)
            {
                _containerPresenter.BindToContainer(containerObject.Container);
                _inventoryPanel.transform.SetParent(_containerInventoryPlaceHolder);
                _inventoryPanel.transform.localPosition = Vector3.zero;
                var rt = _inventoryPanel.GetComponent<RectTransform>();
                rt.offsetMax = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                _inventoryPanel.gameObject.SetActive(true);
                _containerPresenter.gameObject.SetActive(true);
                _containerPresenter.Slot.ItemLeave += ItemLeave;
                _containerPresenter.Slot.ItemPresenterSet += ItemPresenterSet;
                _containerPresenter.Slot.WeaponReloaded += OnWeaponReloaded;
                return;
            }
        }

        if (Input.GetMouseButtonDown(0) && !_mouseOverUI && _canMove && PlayerCanReach(AllignPoint.ToMid(GetPointerPositionOnMap())) 
                   && !AvatarBusy &&!_containerPresenter.gameObject.activeSelf)
        {
            var navPoint = Instantiate(Global.NavPointPrefab);
            navPoint.transform.position = _pointer.position;
            _navPoints.Add(navPoint);

            _lastPoint = _playerAvatar.transform.position;
            _lastAngle = _playerAvatar.transform.eulerAngles;

            var path = new NavMeshPath();

            if (_playerAvatar.CalculateCompletePath(_pointer.position, path))
            {
                //DrawPath(path);
                _playerAvatar.AddMoveQuant(_pointer.position);
                _playerAvatar.MoveTo(_pointer.position);
            }
        }

        if (Input.GetKey(KeyCode.Space))
        {
            ApplyQuants();
        }

        if (Input.GetKey(KeyCode.Backspace)) 
        { 
            ClearLastQuant();
        }
    }

    private Vector3 GetPointerPositionOnMap() 
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Physics.Raycast(ray, out hit);
        return hit.point;
    }

    private ItemObject GetItemUnderMousePoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Physics.Raycast(ray, out hit);
        if (hit.rigidbody != null && hit.rigidbody.gameObject.TryGetComponent<ItemObject>(out var itemObject))
        {
            return itemObject;
        }
        return null;
    }

    private ContainerObject GetContainerUnderMousePoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Physics.Raycast(ray, out hit);
        if (hit.rigidbody != null && hit.rigidbody.gameObject.TryGetComponent<ContainerObject>(out var containerObject))
        {
            return containerObject;
        }
        return null;
    }

    private EntityAvatar GetEntityAvatarUnderMousePoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Physics.Raycast(ray, out hit);
        if (hit.rigidbody != null && hit.rigidbody.gameObject.TryGetComponent<EntityAvatar>(out var entityAvatar))
        {
            return entityAvatar;
        }
        return null;
    }

    private void OnMouseOver()
    {
        if (_pointer != null)
        {
            var pointerPosition = GetPointerPositionOnMap();
            var movePosition = AllignPoint.ToMid(pointerPosition);

            _canMove = PlayerCanReach(movePosition) && !_mouseOverUI;
            _pointer.SetActive(_canMove && !AvatarBusy);
            if (_pointer.activeSelf && _pointer.position != movePosition)
            {
                _pointer.position = movePosition;

                //if (playerController.PlayerCanReach(_pointer.transform.position))
                _pointer.SetPointerMaterial(AccessMoveMaterial);
                //else
                //    _pointer.SetPointerMaterial(RestrictMoveMaterial);
            }
        }
    }

    void OnMouseExit()
    {
        _pointer.SetActive(false);
    }

    //private void DrawPath(NavMeshPath path)
    //{
    //    var startPosition = _pathDrawer.positionCount;
    //    _pathDrawer.positionCount += path.corners.Length;
    //    for (var i = startPosition; i < _pathDrawer.positionCount; i++)
    //    {
    //        _pathDrawer.SetPosition(i, path.corners[i - startPosition]);
    //    }
    //}

    private bool PlayerCanReach(Vector3 point)
    {
        return _playerAvatar.CalculateCompletePath(point, new NavMeshPath());
    }

    private void ApplyQuants()
    {
        RevertAllQuants();
        _playerAvatar.ApplyQuants();        
    }

    private void RevertQuant(Quant quant)
    {
        switch (quant.Action)
        {
            case EntityAction.Move:
                {
                    if (_navPoints.Count > 0)
                    {
                        Destroy(_navPoints[_navPoints.Count - 1]);
                        _navPoints.RemoveAt(_navPoints.Count - 1);
                    }
                    if (quant.LastPosition != null)
                    {
                        _playerAvatar.SetToPosition(quant.LastPosition.Value);
                        _playerAvatar.transform.rotation = quant.LastRotation;
                    }
                    break;
                }
            case EntityAction.PickObject:
                {
                    var itemObject = quant.Object as ItemObject;                    
                    itemObject.transform.SetParent(null);
                    itemObject.transform.position = quant.LastPosition.Value;
                    itemObject.transform.rotation = quant.LastRotation;
                    itemObject.Drop();
                    _playerAvatar.Character.UnEquip(itemObject.Item);
                    itemObject.gameObject.SetActive(true);
                    break;
                }
            case EntityAction.TransferItem:
                {
                    var transferItemInfo = quant.Object as TransferItemInfo;
                    var sourceSlot = transferItemInfo.Source;
                    var destinationSlot = transferItemInfo.Destination;
                    var itemPresenter = transferItemInfo.ItemPresenter;

                    _playerAvatar.TransferItem(destinationSlot, sourceSlot, itemPresenter);

                    if (destinationSlot is CharacterItemSlot characterItemSlot && characterItemSlot.SlotType == SlotType.MainWeapon)
                        _mainWeaponImage.gameObject.SetActive(false);
                    if (sourceSlot is CharacterItemSlot characterItemSlot1 && characterItemSlot1.SlotType == SlotType.MainWeapon)
                    {
                        _mainWeaponImage.gameObject.SetActive(true);
                        _mainWeaponImage.sprite = Global.GetIconFor(itemPresenter.Item.GetType());
                    }

                    break;
                }
                case EntityAction.ReloadWeapon:
                {
                    var reloadWeaponInfo = quant.Object as ReloadWeaponInfo;
                    var weapon = reloadWeaponInfo.WeaponPresenter.Item as RangeWeapon;
                    var loadedAmmo = reloadWeaponInfo.AmmoUsed;
                    var sourceSlot = reloadWeaponInfo.SourceSlot;
                    var ammoPresenter = reloadWeaponInfo.AmmoPresenter;
                    if (ammoPresenter == null)
                    {
                        ammoPresenter = ItemFactory.CreateItemPresenter(reloadWeaponInfo.AmmoType);
                        ammoPresenter.Count = loadedAmmo;
                        sourceSlot.InsertItem(ammoPresenter);
                        reloadWeaponInfo.AmmoPresenter = ammoPresenter;
                    }
                    else
                    {
                        ammoPresenter.Count += loadedAmmo;
                    }
                    weapon.UnLoad(loadedAmmo);
                    _playerAvatar.InventoryPresenter.RefreshItemSlots();
                    sourceSlot.FillSlots();

                    break;
                }
            case EntityAction.Attack:
                {
                    var attackInfo = quant.Object as AttackInfo;
                    if (_playerAvatar.Character.MainWeapon is RangeWeapon rangedWeapon)
                    {
                        rangedWeapon.Reload(attackInfo.AmmoType, attackInfo.AmmoCount);
                        _playerAvatar.InventoryPresenter.RefreshItemSlots();
                    }
                    if (quant.LastPosition != null)
                    {
                        _playerAvatar.SetToPosition(quant.LastPosition.Value);
                        _playerAvatar.transform.rotation = quant.LastRotation;
                    }
                    Destroy(_targets[_targets.Count - 1]);
                    _targets.RemoveAt(_targets.Count - 1);
                    break;
                }
        }
    }

    private void RevertLastQuant()
    {
        if (_playerAvatar.Quants.Count > 0)
            RevertQuant(_playerAvatar.Quants.Last());
    }

    private void RevertAllQuants()
    {
        if (AvatarBusy)
            return;
        _quantsReverting = true;
        _playerAvatar.Quants.Reverse();
        foreach (var quant in _playerAvatar.Quants) 
        {
            RevertQuant(quant);
        }
        _playerAvatar.Quants.Reverse();
        _quantsReverting = false;
    }

    private void ClearLastQuant()
    {
        if (AvatarBusy)
            return;
        RevertLastQuant();
        _playerAvatar.RemoveLastQuant();
    }

    public void ClearAllQuants()
    {
        if (AvatarBusy)
            return;
        _quantsReverting = true;
        do
        {
            ClearLastQuant();
        }
        while (_playerAvatar.Quants.Any());
        _quantsReverting = false;
    }

    public void ButtonApplyQuantsClick()
    {
        if (AvatarBusy)
            return;
        ApplyQuants();
    }

    public void ButtonClearLastClick()
    {
        if (AvatarBusy)
            return;
        ClearLastQuant();
    }

    public void ButtonClearAll()
    {
        ClearAllQuants();
    }

    public void InventoryPanelSwitch()
    {
        if (_containerPresenter.gameObject.activeSelf)
            return;
        

        if (_inventoryPanel.transform.parent != _originInventoryPlaceHolder)
        {
            _inventoryPanel.transform.SetParent(_originInventoryPlaceHolder, false);
            _inventoryPanel.transform.localPosition = Vector3.zero;
            var rt = _inventoryPanel.GetComponent<RectTransform>();
            rt.offsetMax = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            _inventoryPanel.gameObject.SetActive(true);
        }
        else
            _inventoryPanel.gameObject.SetActive(!_inventoryPanel.gameObject.activeSelf);
    }
    private void ItemLeave(Item item, DropSlot slot)
    {
        if (slot is CharacterItemSlot characterItemSlot && characterItemSlot.SlotType == SlotType.MainWeapon)
        {
            _mainWeaponImage.sprite = null;
            _mainWeaponImage.gameObject.SetActive(false);
            if (item is RangeWeapon rangeWeapon)
            {
                rangeWeapon.AmmoChanged -= ChangeAmmo;
                _mainWeaponAmmo.gameObject.SetActive(false);
            }
        }
    }

    public void ItemPresenterSet(DropSlot sourceSlot, DropSlot destinationSlot, ItemPresenter itemPresenter)
    {
        if (sourceSlot == destinationSlot)
            return;
        if (!_avatarApplyingQants && !_quantsReverting)
        {
            var transferItemInfo = new TransferItemInfo(sourceSlot, destinationSlot, itemPresenter);
            _playerAvatar.AddItemtransferQuant(transferItemInfo);
        }
        if (destinationSlot is CharacterItemSlot characterItemSlot && characterItemSlot.SlotType == SlotType.MainWeapon)
        {
            ReflectMainWeapon(itemPresenter.Item);
        }
    }

    private void ReflectMainWeapon(Item item)
    {
        if (item == null) 
            return;
        _mainWeaponImage.sprite = Global.GetIconFor(item.GetType());
        _mainWeaponImage.gameObject.SetActive(true);
        if (item is RangeWeapon rangeWeapon)
        {
            rangeWeapon.AmmoChanged += ChangeAmmo;
            ChangeAmmo(rangeWeapon, rangeWeapon.CurrentAmmoType, rangeWeapon.AmmoCount);
        }
        else
            _mainWeaponAmmo.gameObject.SetActive(false);
    }

    private void ChangeAmmo(RangeWeapon weapon, Type ammoType, int num)
    {
        if (ammoType != null && weapon.AmmoCount + num > 0)
        {
            _mainWeaponAmmo.sprite = Global.GetIconFor(ammoType);
            _mainWeaponAmmo.gameObject.SetActive(true);
        }
        else
            _mainWeaponAmmo.gameObject.SetActive(false);
    }

    public void ContainerCloseClick()
    {
        _containerPresenter.Slot.ItemLeave -= ItemLeave;
        _containerPresenter.Slot.ItemPresenterSet -= ItemPresenterSet;
        _containerPresenter.Slot.WeaponReloaded -= OnWeaponReloaded;
    }

    public void OnWeaponReloaded(ItemPresenter weaponPresenter, ItemPresenter ammoPresenter, int num, StorageSlot slot)
    {
        var quantInfo = new ReloadWeaponInfo(weaponPresenter, ammoPresenter, num, slot);
        _playerAvatar.AddReloadWeaponQuant(quantInfo);
    }

    public void SetFireMode(int mode)
    {
        OnFireModeSet((FireMode)mode);
    }

    public void OnFireModeSet(FireMode fireMode)
    {
        var firemode1Available = (_playerAvatar.Character.MainWeapon is RangeWeapon rangeWeapon1) && rangeWeapon1.SingleShot != null;
        var firemode2Available = (_playerAvatar.Character.MainWeapon is RangeWeapon rangeWeapon2) && rangeWeapon2.ShortBurst != null;
        var firemode3Available = (_playerAvatar.Character.MainWeapon is RangeWeapon rangeWeapon3) && rangeWeapon3.LongBurst != null;

        _SetFireMode1.GetComponent<EventTrigger>().enabled = firemode1Available;
        _SetFireMode2.GetComponent<EventTrigger>().enabled = firemode2Available;
        _SetFireMode3.GetComponent<EventTrigger>().enabled = firemode3Available;

        _SetFireMode1.interactable = firemode1Available;
        _SetFireMode2.interactable = firemode2Available;
        _SetFireMode3.interactable = firemode3Available;

        _SetFireMode1.isOn = fireMode == FireMode.SingleShot;
        _SetFireMode2.isOn = fireMode == FireMode.ShortBurst;
        _SetFireMode3.isOn = fireMode == FireMode.LongBurst;


        SetFireMode(fireMode);
    }

    public void SetFireMode(FireMode fireMode)
    {
        _playerAvatar.FireMode = fireMode;
    }

}
