using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LylekGames.Tools
{
    public class DoorOpen : MonoBehaviour
    {
        private Vector3 startRot;

        private Vector3 newRot;

        private bool open = false;

        private bool rotating = false;

        public void Start()
        {
            startRot = transform.eulerAngles;
        }
        public void Interact()
        {
            if (!open)
            {
                newRot = startRot + new Vector3(0, 90, 0);

                open = true;
            }
            else
            {
                newRot = startRot;

                open = false;
            }

            if (!rotating)
            {
                rotating = true;

                StartCoroutine(BeginRotate());
            }
        }
        private IEnumerator BeginRotate()
        {
            float distance = Vector3.Distance(transform.eulerAngles, newRot);

            while (transform.eulerAngles != newRot)
            {
                transform.eulerAngles = Vector3.MoveTowards(transform.eulerAngles, newRot, 100 * Time.deltaTime);

                distance = Vector3.Distance(transform.eulerAngles, newRot);

                if (distance < 0.1)
                {
                    break;
                }

                yield return null;
            }

            rotating = false;
        }
    }
}