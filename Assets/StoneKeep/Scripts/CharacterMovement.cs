using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LylekGames.Tools
{
    public class CharacterMovement : MonoBehaviour
    {
        public CharacterController controller;

        public float speed = 3;

        private Vector3 gravity = new Vector3(0, -9.81f, 0);

        public void Update()
        {
            controller.Move(gravity * Time.deltaTime);

            if (Input.GetKey(KeyCode.W))
            {
                controller.Move(transform.forward * speed * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.A))
            {
                controller.Move(-transform.right * speed * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.D))
            {
                controller.Move(transform.right * speed * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.S))
            {
                controller.Move(-transform.forward * speed * Time.deltaTime);
            }
        }
    }
}