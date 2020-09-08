using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using UnityEngine.UIElements;
using Cinemachine;
using NerScript;
using Random = UnityEngine.Random;

/// <summary>
/// カメラの移動を司る
/// </summary>
public class TutorialCameraMove : MonoBehaviour
{
    [SerializeField] private OrderManager odermanager;
    [SerializeField] private GameObject targetStore;
    private GameObject targetCustomer;

    [SerializeField] GameObject parent;

    PlayerFacade playerFacade;

    Vector3 parentPosition;

    Vector3 storePosition;

    [SerializeField, Tooltip("視野角")] float view = 90.0f;

    [SerializeField] float railPositionX;

    [SerializeField] Vector3 cameraFrontPositionValue = new Vector3(0, 2, -5);

    [SerializeField, Tooltip("ジャンプ中の回転角度")]
    float jumpCinemaAngle = 40;

    [SerializeField, Tooltip("回転軸")] Vector3 axis = new Vector3(0, 1, 0);

    [SerializeField, Tooltip("Lerpに必要な秒数")]
    float attenRateTime = 0.3f;

    [SerializeField, Tooltip("補間に必要な割合")] float attenRate = 2.0f;

    [SerializeField, Tooltip("リザルトの開始までの時間")]
    private float resultStartTime = 5.0f;

    //Lerp開始を告げる変数
    bool lerp;
    bool lerpEnd;
    bool afterJump;

    bool deliveryFlag;
    bool normalFlag;
    bool boost;

    float startTime;
    float lerpRate;
    Vector3 startPosition;

    CinemachineVirtualCamera virtualCamera;

    [SerializeField, Tooltip("加速時カメラ倍率")] float magnification = 0.5f;

    PlayerMove playerMove;

    Vector3 position;
    Vector3 parentVector;

    private Vector3 rotateAxis;

    private bool startResult;
    private bool processEndFlag;

    private bool tutorialProcess;

    // /// <summary>
    // /// 遮蔽物のレイヤー名のリスト。
    // /// 現時点では複数の予定は無いのでコメントアウト
    // /// </summary>
    // [SerializeField]
    // List<string> coverLayerNameList_;

    /// <summary>
    /// 遮蔽物とするレイヤーマスク。
    /// </summary>
    int layerMask;

    /// <summary>
    /// 今回の Update で検出された遮蔽物の Renderer コンポーネント。
    /// </summary>
    List<SkinnedMeshRenderer> rendererHitsList = new List<SkinnedMeshRenderer>();

    /// <summary>
    /// 前回の Update で検出された遮蔽物の Renderer コンポーネント。
    /// 今回の Update で該当しない場合は、遮蔽物ではなくなったので Renderer コンポーネントを有効にする。
    /// </summary>
    public SkinnedMeshRenderer[] rendererHitsPrevs;

    // Start is called before the first frame update
    void Start()
    {
        virtualCamera = transform.GetComponent<CinemachineVirtualCamera>();
        virtualCamera.m_Lens.FieldOfView = view;

        tutorialProcess = true;

        lerp = false;
        lerpEnd = false;
        afterJump = false;
        deliveryFlag = false;
        normalFlag = true;
        boost = false;
        startResult = false;
        processEndFlag = false;

        startTime = 0;
        lerpRate = 0;
        startPosition = transform.position;

        playerFacade = parent.GetComponent<PlayerFacade>();

        parentPosition = parent.transform.position;
        storePosition = new Vector3(railPositionX, parent.transform.position.y, parent.transform.position.z) +
                        transform.right * cameraFrontPositionValue.x + transform.up * cameraFrontPositionValue.y +
                        transform.forward * cameraFrontPositionValue.z;
        position = storePosition;

        playerMove = parent.GetComponent<PlayerMove>();
        parentVector = playerMove.Velocity;

        transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                             parent.transform.up * cameraFrontPositionValue.y +
                             transform.forward * cameraFrontPositionValue.z;

        //UniRxに登録
        playerFacade.OnStateChange(PlayerState.State.Jump).Subscribe(flag => ChangeStateToJump(flag));
        playerFacade.OnStateChange(PlayerState.State.Normal).Subscribe(flag => ChangeStateToNormal(flag));
        playerFacade.OnStateChange(PlayerState.State.Delivery).Subscribe(flag => ChangeStateToDelivery(flag));
        playerFacade.OnStateChange(PlayerState.State.Boost).Subscribe(flag => ChangeStateToBoost(flag));

        layerMask = LayerMask.GetMask("Walker");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (StageManager.StageManagerObject.isEnd)
        {
            Result();
        }
        else
        {
            if (!(StageManager.StageManagerObject.tutorialState == 4 ||
                  StageManager.StageManagerObject.tutorialState == 1))
            {
                FollowPlayerVector(parentVector);
                JumpCinemaCamera(position);
                Boost();
                RayCast();
                Delivery();
            }

            Tutorial();

            parentVector = playerMove.Velocity;
            parentPosition = parent.transform.position;
        }
    }

    void Tutorial()
    {
        int state = StageManager.StageManagerObject.tutorialState;

        if (state == 1)
        {
            if (tutorialProcess)
            {
                StartCoroutine(TargetCoroutine(targetStore.transform.position));
                tutorialProcess = false;
            }
        }
        else if (state == 4)
        {
            if (tutorialProcess)
            {
                targetCustomer = odermanager.tutorialObj;
                StartCoroutine(TargetCoroutine(targetCustomer.transform.position));
                tutorialProcess = false;
            }
        }
    }

    IEnumerator TargetCoroutine(Vector3 position)
    {
        LookAtTargetCameraHeight(position);

        float seconds = 1.0f;
        
        while (seconds > 0.0f)
        {
            virtualCamera.m_Lens.FieldOfView -= Time.deltaTime * 30.0f;
            seconds -= Time.deltaTime;
            
            yield return null;
        }
        
        //OnCompleteが呼ばれる処理でないと無限ループする
        //yield return Observable.Timer(TimeSpan.FromSeconds(1)).ToYieldInstruction();

        StartCoroutine(StayCoroutine());
    }

    IEnumerator StayCoroutine()
    {
        //OnCompleteが呼ばれる処理でないと無限ループする
        yield return Observable.Timer(TimeSpan.FromSeconds(2)).ToYieldInstruction();

        StartCoroutine(EndCorutine());
    }

    IEnumerator EndCorutine()
    {
        LookAtTargetCameraHeight(position);

        float seconds = 1.0f;
        
        while (seconds > 0.0f)
        {
            virtualCamera.m_Lens.FieldOfView += Time.deltaTime * 30.0f;
            seconds -= Time.deltaTime;
            
            yield return null;
        }
        //OnCompleteが呼ばれる処理でないと無限ループする
        //yield return Observable.Timer(TimeSpan.FromSeconds(1)).ToYieldInstruction();

        tutorialProcess = true;
        StageManager.StageManagerObject.tutorialState++;
    }

    void Result()
    {
        if (!processEndFlag)
        {
            parentPosition = parent.transform.position;

            transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                 parent.transform.up * cameraFrontPositionValue.y +
                                 parent.transform.forward * (-cameraFrontPositionValue.z);
            LookAtTargetCameraHeight(parentPosition);

            StartCoroutine(StartResultCoroutine());

            processEndFlag = true;
        }

        if (startResult)
        {
            transform.position = Vector3.Lerp(transform.position,
                parentPosition + parent.transform.right * -2.0f +
                parent.transform.up * cameraFrontPositionValue.y
                + parent.transform.forward * -cameraFrontPositionValue.z,
                Time.deltaTime * 2.0f);
        }
    }

    IEnumerator StartResultCoroutine()
    {
        //使い回し
        //OnCompleteが呼ばれる処理でないと無限ループする
        yield return Observable.Timer(TimeSpan.FromSeconds(5.0f)).ToYieldInstruction();
        startResult = true;
    }

    void RayHit()
    {
        RaycastHit hit;

        Vector3 standardPosition = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                   parent.transform.up * cameraFrontPositionValue.y +
                                   parent.transform.forward * cameraFrontPositionValue.z;

        if (Physics.Linecast(parentPosition, standardPosition, out hit, LayerMask.GetMask("Wall")))
        {
            position = Vector3.Lerp(hit.point, position, Time.deltaTime * attenRate);

            LookAtTargetCameraHeight(parentPosition);
        }
    }

    void Avoid()
    {
        Vector3 standardPosition = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                   parent.transform.up * cameraFrontPositionValue.y +
                                   parent.transform.forward * cameraFrontPositionValue.z;

        //発射点と方向の更新
        Ray rayRight = new Ray(position, transform.TransformDirection(Vector3.right));
        Ray rayLeft = new Ray(position, transform.TransformDirection(Vector3.left));


        RaycastHit hitRight, hitLeft, hitBack;

        bool right = Physics.Raycast(rayRight.origin, rayRight.direction, out hitRight, 3.0f,
            LayerMask.GetMask("Wall"));
        bool left = Physics.Raycast(rayLeft.origin, rayLeft.direction, out hitLeft, 3.0f, LayerMask.GetMask("Wall"));
        bool back = Physics.Linecast(parentPosition, standardPosition, out hitBack, LayerMask.GetMask("Wall"));

        if (right || left || back)
        {
            // if (right)
            // {
            //     position = Vector3.Lerp(hitRight.point + transform.TransformDirection(Vector3.left) * 3.0f,
            //         transform.position, Time.deltaTime * 10.0f);
            // }
            //
            // if (left)
            // {
            //     position = Vector3.Lerp(hitLeft.point + transform.TransformDirection(Vector3.right) * 3.0f,
            //         transform.position, Time.deltaTime * 10.0f);
            // }

            if (back)
            {
                Vector3 direction = parent.transform.position - hitBack.point;
                float directionMag = 5.0f - direction.magnitude;

                position = Vector3.Lerp(position,
                    parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                    parent.transform.up * (cameraFrontPositionValue.y + directionMag * 1.5f) +
                    parent.transform.forward * (cameraFrontPositionValue.z + Mathf.Clamp(directionMag, 0, 5.0f)),
                    Time.deltaTime * 10.0f);
            }

            transform.LookAt(parentPosition);
            transform.position = position;
        }
    }

    void FollowPlayerVector(Vector3 vector)
    {
        if (playerFacade.GetState(PlayerState.State.Normal) || playerFacade.GetState(PlayerState.State.Boost))
        {
            if (vector.magnitude > 1.0f)
            {
                position = Vector3.Lerp(position,
                    parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                    parent.transform.up * cameraFrontPositionValue.y + transform.forward * cameraFrontPositionValue.z,
                    Time.deltaTime * 10.0f);
                virtualCamera.m_Lens.FieldOfView = view + magnification * vector.magnitude;
            }
            else
            {
                virtualCamera.m_Lens.FieldOfView = view;
                position = Vector3.Lerp(position,
                    parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                    parent.transform.up * cameraFrontPositionValue.y +
                    parent.transform.forward * cameraFrontPositionValue.z, Time.deltaTime * 3.0f);
            }

            LookAtTargetCameraHeight(parentPosition);

            RayHit();

            transform.position = position;
        }
        else if (playerFacade.GetState(PlayerState.State.Brake))
        {
            position = Vector3.Lerp(position,
                parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                parent.transform.up * cameraFrontPositionValue.y +
                parent.transform.forward * cameraFrontPositionValue.z, Time.deltaTime * 10.0f);
            LookAtTargetCameraHeight(parentPosition);

            RayHit();

            transform.position = position;
        }
    }

    void RayCast()
    {
        /****ぶつかったオブジェクトを透過する処理が必要****/
        //https://qiita.com/sakura-crowd/items/3608b2fd6df8a953240a

        //Vector3 direction = new Vector3(parentPosition.x,transform.position.y,parentPosition.z) - transform.position;

        //当たり判定情報
        RaycastHit tempHit;

        //発射点と方向の更新
        Ray ray = new Ray(transform.position, transform.forward);

        // 前回の結果を退避してから、Raycast して今回の遮蔽物のリストを取得する
        RaycastHit[] hits = Physics.RaycastAll(ray, 3.0f, layerMask);

        rendererHitsPrevs = rendererHitsList.ToArray();
        rendererHitsList.Clear();

        // 遮蔽物は一時的にすべて描画機能を無効にする。
        foreach (RaycastHit hit in hits)
        {
            // 遮蔽物が被写体の場合は例外とする
            if (hit.collider.gameObject == parent)
            {
                continue;
            }

            // 遮蔽物の Renderer コンポーネントを無効にする
            SkinnedMeshRenderer renderer = hit.collider.gameObject.GetComponent<SkinnedMeshRenderer>();

            if (renderer != null)
            {
                rendererHitsList.Add(renderer);
                renderer.enabled = false;
            }
        }

        // 前回まで対象で、今回対象でなくなったものは、表示を元に戻す。
        foreach (SkinnedMeshRenderer renderer in rendererHitsPrevs.Except<SkinnedMeshRenderer>(rendererHitsList))
        {
            // 遮蔽物でなくなった Renderer コンポーネントを有効にする
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
    }

    void LookAtTargetCameraHeight(Vector3 target)
    {
        transform.LookAt(new Vector3(target.x, transform.position.y, target.z));
    }

    // StateがJumpに変わった瞬間に取る
    // StateがJumpに変わった瞬間に取る
    void ChangeStateToJump(bool flag)
    {
        //正規化した軸を利用して回転を求める
        if (flag && !afterJump)
        {
            int number = Random.Range(0, 5);

            switch (number)
            {
                case 0:
                    transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                         parent.transform.up * cameraFrontPositionValue.y +
                                         parent.transform.forward * cameraFrontPositionValue.z;
                    
                    transform.RotateAround(transform.position, axis.normalized, jumpCinemaAngle);

                    rotateAxis = axis.normalized;
                    break;
                case 1:
                    transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                         parent.transform.up * cameraFrontPositionValue.y +
                                         parent.transform.forward * cameraFrontPositionValue.z;
                    
                    transform.RotateAround(transform.position, axis.normalized, jumpCinemaAngle * 1.5f);
                    rotateAxis = axis.normalized;
                    break;
                case 2:
                    transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                         parent.transform.up * cameraFrontPositionValue.y +
                                         parent.transform.forward * cameraFrontPositionValue.z;

                    rotateAxis = new Vector3(0.0f, 1.0f, 0.7f);
                    
                    Debug.Log("今です");
                    transform.RotateAround(transform.position, rotateAxis.normalized, -jumpCinemaAngle - 20.0f);
                    break;
                case 3:
                    transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                         parent.transform.up * cameraFrontPositionValue.y +
                                         parent.transform.forward * cameraFrontPositionValue.z;
                    
                    rotateAxis = new Vector3(0.0f, 1.0f, 0.25f);

                    transform.RotateAround(transform.position, rotateAxis.normalized, 20.0f);
                    break;
                case 4:
                    transform.position = parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                                         parent.transform.up * cameraFrontPositionValue.y +
                                         transform.forward * cameraFrontPositionValue.z;

                    rotateAxis = new Vector3(0.0f, 1.0f, 0.0f);

                    transform.RotateAround(transform.position, rotateAxis.normalized, 180.0f);
                    break;
            }

            afterJump = true;
        }
    }

    //ジャンプ中とジャンプ終了直後のカメラ
    void JumpCinemaCamera(Vector3 position)
    {
        if (afterJump)
        {
            //lerp処理の開始 ジャンプが終了するまで待つ
            if (lerp && !lerpEnd)
                Lerp(position);
            else
            {
                position =
                    parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                    transform.forward * cameraFrontPositionValue.z;
                transform.position = position;
            }

            transform.RotateAround(transform.position, rotateAxis, (-jumpCinemaAngle / 2) * Time.deltaTime);
        }
    }

    //Stateが通常に戻ったとき
    void ChangeStateToNormal(bool flag)
    {
        if (flag)
        {
            startTime = Time.time;
            startPosition = transform.position;

            //ステートが通常時に戻ったか
            lerp = true;
            lerpEnd = false;
            deliveryFlag = false;
            boost = false;

            //コルーチンの開始
            StartCoroutine(ChangeToFollowCameraCoroutine());
        }
    }

    //ジャンプ終了直後からの処理
    void Lerp(Vector3 position)
    {
        //補間処理の割合
        lerpRate = (Time.time - startTime) / attenRateTime;
        //補間して指定のposition
        transform.position = Vector3.Lerp(startPosition, position, lerpRate);
    }

    IEnumerator ChangeToFollowCameraCoroutine()
    {
        //lerpする時間だけ処理を待つ(UniRx版 UniRxのストリームをコルーチンとして利用)
        //OnCompleteが呼ばれる処理でないと無限ループする
        yield return Observable.Timer(TimeSpan.FromSeconds(attenRateTime)).ToYieldInstruction();

        afterJump = false;
        lerpEnd = true;
        normalFlag = true;
        boost = false;
    }

    //一瞬で注文者視点に（未）
    void ChangeStateToDelivery(bool flag)
    {
        if (flag)
        {
            position = new Vector3(playerFacade.DeliveryTarget.position.x,
                playerFacade.DeliveryTarget.position.y + 1.5f, playerFacade.DeliveryTarget.position.z);
            transform.position = position;
            LookAtTargetCameraHeight(parent.transform.position);
            deliveryFlag = true;
        }
    }

    //終わったらLerpで元の位置に
    void Delivery()
    {
        if (deliveryFlag)
        {
            normalFlag = false;
            LookAtTargetCameraHeight(parent.transform.position);
        }
        else if (!normalFlag)
        {
            position = Vector3.Lerp(position,
                parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                parent.transform.up * cameraFrontPositionValue.y +
                parent.transform.forward * cameraFrontPositionValue.z, Time.deltaTime * 3.0f);
        }
    }

    void ChangeStateToBoost(bool flag)
    {
        if (flag)
        {
            virtualCamera.m_Lens.FieldOfView = view + 20.0f;

            var impalse = GetComponent<CinemachineImpulseSource>();
            impalse.GenerateImpulse();
            boost = true;
            StartCoroutine(ChangeToNormalViewCoroutine());
        }
    }

    void Boost()
    {
        if (boost)
        {
            position = Vector3.Lerp(position,
                parentPosition + parent.transform.right * cameraFrontPositionValue.x +
                parent.transform.up * cameraFrontPositionValue.y + transform.forward * cameraFrontPositionValue.z,
                Time.deltaTime * 10.0f);
            transform.position = position;

            LookAtTargetCameraHeight(parentPosition);

            //LateUpdateなのでTime.deltaTimeが要らない
            virtualCamera.m_Lens.FieldOfView -= 0.1f;

            var impalse = GetComponent<CinemachineImpulseSource>();
            impalse.GenerateImpulse();
        }
    }

    IEnumerator ChangeToNormalViewCoroutine()
    {
        //使い回し
        //OnCompleteが呼ばれる処理でないと無限ループする
        yield return Observable.Timer(TimeSpan.FromSeconds(1)).ToYieldInstruction();

        virtualCamera.m_Lens.FieldOfView = view;
    }

    //一定の距離以上で一定の距離以内だったらArchの方を向く
    //ただしこの時カメラがArchの方を向いていなければならない
    //まだArch出来てないんで待ち
    void Arch()
    {
    }
}