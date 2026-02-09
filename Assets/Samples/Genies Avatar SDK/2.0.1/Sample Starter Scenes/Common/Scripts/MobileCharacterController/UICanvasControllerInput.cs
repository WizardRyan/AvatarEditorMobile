
using UnityEngine;

namespace Genies.Sdk.Samples.Common
{
    public class UICanvasControllerInput : MonoBehaviour
    {
       public GeniesInputs GeniesInputs { get; private set; }

        private void Start()
        {
            // Cache FindObjectOfType result in Start instead of OnEnable for better performance
            if (GeniesInputs == null)
            {
                GeniesInputs = FindObjectOfType<GeniesInputs>();
            }

            if (GeniesInputs == null)
            {
                Debug.LogWarning("GeniesInputs not found! Disabling UICanvasControllerInput.", this);
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Validate cached reference is still valid
            if (GeniesInputs == null)
            {
                GeniesInputs = FindObjectOfType<GeniesInputs>();
            }

            if (GeniesInputs == null)
            {
                gameObject.SetActive(false);
            }
        }

        public void VirtualMoveInput(Vector2 virtualMoveDirection)
        {
            if (GeniesInputs != null)
            {
                GeniesInputs.MoveInput(virtualMoveDirection);
            }
        }

        public void VirtualLookInput(Vector2 virtualLookDirection)
        {
            if (GeniesInputs != null)
            {
                GeniesInputs.LookInput(virtualLookDirection);
            }
        }

        public void VirtualJumpInput(bool virtualJumpState)
        {
            if (GeniesInputs != null)
            {
                GeniesInputs.JumpInput(virtualJumpState);
            }
        }

        public void VirtualSprintInput(bool virtualSprintState)
        {
            if (GeniesInputs != null)
            {
                GeniesInputs.SprintInput(virtualSprintState);
            }
        }
    }
}
