using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    const char CHAR_TERMINATOR = ';';
    const char CHAR_COMMA = ',';
    const int DAMAGE_ATTACK = 30;

    private SocketModule tcp;

    [SerializeField]
    private InputField nickname;
    string myID;

    public GameObject prefabUnit;
    public GameObject mainChar;
    public GameObject agarPrefab;

    private UnitControl mainControl;

    Dictionary<string, UnitControl> remoteUnits;
    Queue<string> commandQueue;
    List<GameObject> agars = new List<GameObject>();


    private void Start()
    {
        tcp = GetComponent<SocketModule>();
        remoteUnits = new Dictionary<string, UnitControl>();
        commandQueue = new Queue<string>();

        mainControl = mainChar.GetComponent<UnitControl>();

        //StartCoroutine(CoMakeAgar());
    }

    private void Update()
    {
        ProcessQueue();

        if (Input.GetMouseButtonDown(0))
        {
            // UI ������ �� �����̴� ���� �����ϱ� ����
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 targetPos;

                // orgPos = transform.position;
                targetPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                // targetPos.z = orgPos.z;
                mainChar.GetComponent<UnitControl>().SetTargetPos(targetPos);

                string data = "#Move#" + targetPos.x + "," + targetPos.y;
                SendCommand(data);
            }
        }

        Attack();
    }

    private void Attack()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (mainControl.GetHP() > 0)
                {
                    string data = "#Attack#";
                    SendCommand(data);
                    mainControl.StartFX();
                }
            }
        }
    }

    public void RemoveAgar(GameObject targetObj)
    {
        
        //SendCommand()
    }

    private void ProcessQueue()
    {
        while (commandQueue.Count > 0)
        {
            string nextCommand = commandQueue.Dequeue();
            ProcessCommand(nextCommand);
        }
    }

    public void OnLogin()
    {
        string id = nickname.text;
        myID = id;

        if (id.Length > 0)
        {
            tcp.Login(id);
            mainChar.transform.position = Vector3.zero;
        }
    }

    public void OnLogOut()
    {
        tcp.Logout();

        foreach (var remotePair in remoteUnits)
        {
            Destroy(remotePair.Value.gameObject);
        }
        Transform [] items = GetComponentsInChildren<Transform>();

        
        foreach(Transform item in items)
        {
            if (item != items[0])  Destroy(item.gameObject);
        }

        remoteUnits.Clear();
    }

    public void OnRevive()
    {
        mainChar.GetComponent<UnitControl>().Revive();

        string data = "#Heal#";
        SendCommand(data);
    }

    public UnitControl AddUnit(string id)
    {
        UnitControl uc = null;

        if (!remoteUnits.ContainsKey(id))
        {
            GameObject newUnit = Instantiate(prefabUnit);
            uc = newUnit.GetComponent<UnitControl>();

            remoteUnits.Add(id, uc);
        }

        return uc;
    }

    public void SetMove(string id, string coordinates)
    {
        if (remoteUnits.ContainsKey(id))
        {
            UnitControl uc = remoteUnits[id];

            var strs = coordinates.Split(',');

            Vector3 pos = new Vector3(float.Parse(strs[0]), float.Parse(strs[1]), 0);
            uc.SetTargetPos(pos);
        }
    }

    public void UserLeft(string id)
    {
        if (remoteUnits.ContainsKey(id))
        {
            UnitControl uc = remoteUnits[id];
            Destroy(uc.gameObject);
            remoteUnits.Remove(id);
        }
    }

    public void UserHeal(string id)
    {
        if (remoteUnits.ContainsKey(id))
        {
            UnitControl uc = remoteUnits[id];
            uc.Revive();
        }
    }

    public void UserAttack(string id)
    {
        if (remoteUnits.ContainsKey(id))
        {
            UnitControl uc = remoteUnits[id];
            uc.StartFX();
        }
    }

    private void TakeDamage(string remain)
    {
        var strs = remain.Split(CHAR_COMMA);
        for (int i = 0; i < strs.Length; i++)
        {
            if (remoteUnits.ContainsKey(strs[i]))
            {
                UnitControl uc = remoteUnits[strs[i]];
                if (uc != null)
                {
                    uc.DropHP(DAMAGE_ATTACK);
                    Debug.Log("He Attack");
                }
            }
            else
            {
                if (myID.CompareTo(strs[i]) == 0)
                {
                    mainControl.DropHP(DAMAGE_ATTACK);
                    Debug.Log("I'm Attack");
                }
            }
        }
    }

    private void LoadHistory(string history)
    {
        var strs = history.Split(',');
        int max = strs.Length;

        for (int i = 0; i + 2 < max; i += 3)
        {
            string id = strs[i];

            if (myID.CompareTo(id) != 0)
            {
                UnitControl uc = AddUnit(id);

                if (uc != null)
                {
                    float x = float.Parse(strs[i + 1]);
                    float y = float.Parse(strs[i + 2]);
                    uc.transform.position = new Vector3(x, y, 0f);
                }
            }
        }
    }

    private void LoadHistory_Agar()
    {
        foreach(var item in agars)
        {

        }
    }

    private void SendCommand(string cmd)
    {
        SocketModule.GetInstance().SendData(cmd);
        Debug.Log("cmd sent :" + cmd);
    }

    public string GetID(string cmd)
    {
        int idx = cmd.IndexOf("$");
        string id = "";

        if (idx > 0)
        {
            id = cmd.Substring(0, idx);
        }

        return id;
    }

    public void QueueCommand(string cmd)
    {
        commandQueue.Enqueue(cmd);
    }


    private void ProcessCommand(string cmd)
    {
        bool bMore = true;

        while (bMore)
        {
            Debug.Log("process cmd = " + cmd);
            int idx = cmd.IndexOf("$");
            string id = "";
            if (idx > 0)
            {
                id = cmd.Substring(0, idx);
            }

            int idx2 = cmd.IndexOf("#");
            if (idx2 > idx)
            {
                int idx3 = cmd.IndexOf("#", idx2 + 1);
                if (idx3 > idx2)
                {
                    string command = cmd.Substring(idx2 + 1, idx3 - idx2 - 1);
                    string remain = "";
                    string nextCommand;

                    int idx4 = cmd.IndexOf(CHAR_TERMINATOR, idx3 + 1);

                    if (idx4 > idx3)
                    {
                        remain = cmd.Substring(idx3 + 1, idx4 - idx3 - 1);
                        nextCommand = cmd.Substring(idx4 + 1);
                    }
                    else
                    {
                        remain = cmd.Substring(idx3 + 1, cmd.Length - idx3 - 1);
                        nextCommand = cmd.Substring(idx3 + 1);
                    }

                    if (nextCommand == remain) nextCommand = "";

                    //string remain = cmd.Substring(idx3 + 1);
                    Debug.Log("command = " + command + " id = " + id + " remain = " + remain + " next = " + nextCommand);
                    
                    if (myID.CompareTo(id) != 0) 
                    {
                        switch (command)
                        {
                            case "Enter":
                                AddUnit(id);
                                break;

                            case "Move":
                                SetMove(id, remain);
                                break;

                            case "Left":
                                UserLeft(id);
                                break;

                            case "History":
                                LoadHistory(remain);
                                break;

                            case "Heal":
                                UserHeal(id);
                                break;

                            case "Attack":
                                UserAttack(id);
                                break;

                            case "Damage":
                                print("Attack remain : " + remain);
                                TakeDamage(remain);
                                break;
                            case "MakeAgar":
                                string[] agarDatas = remain.Split(',');
                                try
                                {
                                    GameObject obj = Instantiate(agarPrefab, new Vector3(float.Parse(agarDatas[1]), float.Parse(agarDatas[2]), float.Parse(agarDatas[3])), Quaternion.identity, this.transform);
                                    int _id = int.Parse(agarDatas[0]); ;
                                    obj.GetComponent<AgarId>().myId = _id;
                                    obj.name = _id.ToString();
                                    agars.Add(obj);
                                }
                                catch(Exception e)
                                {
                                    Debug.Log(e);
                                }
                                break;
                            case "RemoveAgar":

                                break;
                        }
                    }
                    else
                    {
                        Debug.Log("Ignore remote command");
                    }

                    cmd = nextCommand;

                    if (cmd.Length <= 0)
                    {
                        bMore = false;
                    }
                }
                else
                {
                    bMore = false;
                }
            }
            else
            {
                bMore = false;
            }
        }
    }
}