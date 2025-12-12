using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MyPlayer : Player {
    NetworkManager _network;

    void Start() {
        StartCoroutine("CoSendPacket");
        StartCoroutine("setPosSendPacket");
        _network = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
    }

    void Update() {

    }

    IEnumerator CoSendPacket() {
        while (true) {
            yield return new WaitForSeconds(0.01f);

            //C_Input inputPacket = new C_Input();
            //inputPacket.inputX = this.inputX;
            //inputPacket.inputZ = this.inputZ;
            //_network.Send(inputPacket.Write());

            //C_Move movePacket = new C_Move();
            //movePacket.velocityX = this.playerRigidbody.velocity.x;
            //movePacket.velocityY = this.playerRigidbody.velocity.y;
            //movePacket.velocityZ = this.playerRigidbody.velocity.z;
            //movePacket.rotationX = this.playerRigidbody.rotation.x;
            //movePacket.rotationY = this.playerRigidbody.rotation.y;
            //movePacket.rotationZ = this.playerRigidbody.rotation.z;
            //_network.Send(movePacket.Write());

            //C_SetPos setPosPacket = new C_SetPos();
            ////movePacket.posX = UnityEngine.Random.Range(-50, 50);
            ////movePacket.posY = 0;
            ////movePacket.posZ = UnityEngine.Random.Range(-50, 50);
            //setPosPacket.posX = this.transform.position.x;
            //setPosPacket.posY = this.transform.position.y;
            //setPosPacket.posZ = this.transform.position.z;
            //_network.Send(setPosPacket.Write());
        }
    }

    IEnumerator setPosSendPacket() {
        while (true) {
            yield return new WaitForSeconds(5.0f);

            //C_SetPos setPosPacket = new C_SetPos();
            //setPosPacket.posX = this.transform.position.x;
            //setPosPacket.posY = this.transform.position.y;
            //setPosPacket.posZ = this.transform.position.z;
            //_network.Send(setPosPacket.Write());
        }
    }
}
