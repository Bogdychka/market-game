using UnityEngine;

namespace Market.Core
{
    public interface IInteractable
    {
        string PromptText { get; }
        bool CanInteract { get; }
        void Interact(GameObject actor);
    }
}
