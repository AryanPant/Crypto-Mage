using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LylekGames.Tools
{
    public class PlayerInteraction : MonoBehaviour
    {
        public GameObject raycastFrom;

        public KeyCode interactKey;

        private RaycastHit hit;

        public void Update()
        {
            if (Input.GetKeyDown(interactKey))
            {
                if (Physics.Raycast(raycastFrom.transform.position, raycastFrom.transform.forward, out hit, 3f))
                {
                    if (hit.transform.tag == "Interact")
                    {
                        hit.transform.gameObject.SendMessage("Interact");
                    }
                }
            }
        }
    }
}