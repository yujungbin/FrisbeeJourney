using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DiscPreviewDragArea :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField]
    private DiscPreviewIdleAnimation previewAnimation;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (previewAnimation == null)
        {
            return;
        }

        previewAnimation.BeginDrag();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (previewAnimation == null)
        {
            return;
        }

        previewAnimation.Drag(eventData.delta.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (previewAnimation == null)
        {
            return;
        }

        previewAnimation.EndDrag();
    }

    private void OnDisable()
    {
        if (previewAnimation != null)
        {
            previewAnimation.EndDrag();
        }
    }
}