using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonArena
{
    // The row remains a Button for its label/keyboard fallback. A slider is an
    // independent control: its click must terminate here instead of at that row.
    public sealed class MenuSlider024 : Slider, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            // Slider already applied a track click in OnPointerDown. UGUI tracks
            // click and press targets separately; consume the click without a
            // second value change or an ancestor Button activation.
            eventData.Use();
        }

        public override void OnDrag(PointerEventData eventData)
        {
            base.OnDrag(eventData);
            if(eventData.button==PointerEventData.InputButton.Left)
                eventData.eligibleForClick=false;
        }
    }
}
