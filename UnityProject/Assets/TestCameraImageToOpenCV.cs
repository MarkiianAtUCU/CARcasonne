using System;
using System.Collections;
using System.Collections.Generic;
using OpenCVForUnity.Calib3dModule;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Utils = OpenCVForUnity.UnityUtils.Utils;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using UnityEngine.Experimental.Rendering;
using Rect = UnityEngine.Rect;
using NativeWebSocket;

public class TestCameraImageToOpenCV : MonoBehaviour
{
    private bool canProcess = true;
    WebSocket websocket;

    // Start is called before the first frame update
    async void Start()
    {
        Debug.Log("TryinToconnect");
        websocket = new WebSocket("ws://192.168.31.135:5353");

        websocket.OnOpen += () => { Debug.Log("Connection open!"); };

        websocket.OnError += (e) => { Debug.Log("Error! " + e); };

        websocket.OnClose += (e) => { Debug.Log("Connection closed!"); };

        websocket.OnMessage += (bytes) =>
        {
            canProcess = true;
            Debug.Log("OnMessage!");
            Debug.Log(bytes);

            // getting the message as a string
            // var message = System.Text.Encoding.UTF8.GetString(bytes);
            // Debug.Log("OnMessage! " + message);
        };

        // Keep sending messages at every 0.3s
        // InvokeRepeating("SendWebSocketMessage", 0.0f, 0.3f);

        // waiting for messages
        await websocket.Connect();
    }


    // async void SendWebSocketMessage()
    // {
    //    
    // }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }


    Texture2D m_Texture;
    Texture2D tex2d;

    public RenderTexture outTex;
    public ARCameraManager CameraManager;
    public ARCameraBackground bkg;


    public Transform positionAnchor;
    public CustomAnchor anchorRef;
    public Transform positionCamera;
    public RectTransform[] dbg;
    private bool initialSetup = true;
    public bool buttonPressed;
    public int img_counter;


    private bool needPrefab = true;
    public Camera mainCam;

    
    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif

        if (needPrefab)
        {
            positionAnchor = GameObject.FindWithTag("TrackedObject").transform;

            if (positionAnchor)
            {
                anchorRef = positionAnchor.GetComponent<CustomAnchor>();
                needPrefab = false;
            }
        }
    }

    public void StartRecord()
    {
        buttonPressed = true;
        img_counter = 0;
    }


    void OnEnable()
    {
        CameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        CameraManager.frameReceived -= OnCameraFrameReceived;
    }


    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (initialSetup)
        {
            initialSetup = false;
            return;
        }


        Debug.Log("Proj: " + args.projectionMatrix);
        Matrix4x4 ViewportMat = args.displayMatrix.Value;
        Debug.Log("x: " + ViewportMat.m10 + "y: " + ViewportMat.m12);
        Debug.Log("Disp: " + args.displayMatrix);
        if (CameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            StartCoroutine(ProcessImage(image, new Vector2(ViewportMat.m10, ViewportMat.m12)));

            image.Dispose();
        }
    }


    IEnumerator ProcessImage(XRCpuImage image, Vector3 viewportScaling)
    {
        // Create the async conversion request.

        XRCpuImage.ConversionParams conv_params = new XRCpuImage.ConversionParams
        {
            // Use the full image.
            inputRect = new RectInt(0, 0, image.width, image.height),

            // Downsample by 2.
            outputDimensions = new Vector2Int(image.width, image.height),

            // Color image format.
            outputFormat = TextureFormat.RGBA32,

            // Flip across the Y axis.
            transformation = XRCpuImage.Transformation.MirrorY
        };

        var request = image.ConvertAsync(conv_params);

        // Wait for the conversion to complete.
        while (!request.status.IsDone())
            yield return null;

        // Check status to see if the conversion completed successfully.
        if (request.status != XRCpuImage.AsyncConversionStatus.Ready)
        {
            // Something went wrong.
            Debug.LogErrorFormat("Request failed with status {0}", request.status);

            // Dispose even if there is an error.
            request.Dispose();
            yield break;
        }

        // Image data is ready. Let's apply it to a Texture2D.
        var rawData = request.GetData<byte>();

        // Create a texture if necessary.
        if (m_Texture == null)
        {
            m_Texture = new Texture2D(
                request.conversionParams.outputDimensions.x,
                request.conversionParams.outputDimensions.y,
                request.conversionParams.outputFormat,
                false);
        }

        // Copy the image data into the texture.
        m_Texture.LoadRawTextureData(rawData);
        m_Texture.Apply();

        Debug.Log("TEX: " + m_Texture.height + "h " + m_Texture.width + "w");
        Debug.Log("Screen: " + m_Texture.height + "h " + m_Texture.width + "w");


        Mat inputMat = new Mat(image.height, image.width, CvType.CV_8UC4);
        Mat outputMat = new Mat(1500, 1500, CvType.CV_8UC4);
        Utils.fastTexture2DToMat(m_Texture, inputMat);

        if (tex2d == null)
        {
            tex2d = new Texture2D(1500,
                1500, conv_params.outputFormat, false);
        }

        Debug.Log("positionAnchor");
        Debug.Log(positionAnchor);

        Debug.Log("anchorRef");
        Debug.Log(anchorRef);

        int counter = 0;

        Point[] srcPointsVec = new Point[4];
        foreach (var point in anchorRef.getWorldPoints())
        {
            Vector3 screenPoint = mainCam.WorldToScreenPoint(point);
            srcPointsVec[counter] = new Point(screenPoint.y * viewportScaling.y / 3,
                100 - screenPoint.x * viewportScaling.x / 3);
            counter += 1;
        }


        MatOfPoint2f srcPoints = new MatOfPoint2f(new[]
        {
            srcPointsVec[0],
            srcPointsVec[1],
            srcPointsVec[2],
            srcPointsVec[3]
        });


        MatOfPoint2f dstPoints = new MatOfPoint2f(new[]
        {
            new Point(195*1.25, 0),
            new Point(0, 0),
            new Point(0, 280*1.25 ),
            new Point(195*1.25, 280*1.25 ),
        });

        Mat H = Calib3d.findHomography(srcPoints, dstPoints);


        Imgproc.warpPerspective(inputMat, outputMat, H, new Size(1500, 1500));
        
        Utils.fastMatToTexture2D(outputMat, tex2d);
        
        
        if (websocket.State == WebSocketState.Open && canProcess)
        {
            websocket.Send(ImageConversion.EncodeToJPG( tex2d, 50));
            canProcess = false;
        }

        inputMat.Dispose();
        inputMat = null;
        outputMat.Dispose();
        outputMat = null;
        request.Dispose();
    }
}