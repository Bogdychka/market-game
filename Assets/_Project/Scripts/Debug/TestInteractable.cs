using Market.Core;
using UnityEngine;

namespace Market.DebugTools
{
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Interact with cube";
        [SerializeField] private bool canInteract = true;

        public string PromptText => prompt;
        public bool CanInteract => canInteract;

        public void Interact(GameObject actor)
        {
            Debug.Log($"[TestInteractable] {name} interacted by {actor.name}", this);
        }
    }
}
