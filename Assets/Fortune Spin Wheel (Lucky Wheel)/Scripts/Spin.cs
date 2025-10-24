using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace JSG.FortuneSpinWheel
{

    public class Spin : MonoBehaviour
    {
        public float Speed;
        public Vector3 Axis;


        // Update is called once per frame
        void Update()
        {
            transform.rotation = Quaternion.Euler(Time.deltaTime * Speed * Axis) * transform.rotation;
        }
    }
}