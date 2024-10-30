using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Elements.Core;
using Rug.Osc;

namespace VRCFTReceiver
{
  public class OSCClient
  {
    private bool _oscSocketState;
    private static readonly float[] _ftData = new float[(int)ExpressionIndex.Count];
    public static float GetData(ExpressionIndex index) => _ftData[(int)index];

    public OscReceiver receiver { get; private set; }
    private Thread receiveThread;
    private CancellationTokenSource cancellationTokenSource;

    private const int DefaultPort = 9000;

    public static DateTime? LastEyeTracking { get; private set; }
    public static DateTime? LastFaceTracking { get; private set; }

    private const string EYE_PREFIX = "/avatar/parameters/v2/Eye";
    private const string MOUTH_PREFIX = "/avatar/parameters/v2/Mouth";
    private const int RECONNECT_ATTEMPT_DELAY_MS = 5000;
    private DateTime _lastReconnectAttempt;
    private static IPAddress _ip;
    private static int _port;

    public OSCClient(IPAddress ip, int? port = null)
    {
      _ip = ip;
      _port = port ?? DefaultPort;

      receiver = new OscReceiver(_ip, _port);

      for (int i = 0; i < (int)ExpressionIndex.Count; i++)
      {
        _ftData[i] = 0f;
      }

      _oscSocketState = true;
      receiver.Connect();

      cancellationTokenSource = new CancellationTokenSource();
      receiveThread = new Thread(ListenLoop);
      receiveThread.Start(cancellationTokenSource.Token);
    }

    private void ListenLoop(object obj)
    {
      UniLog.Log("[VRCFTReceiver] Started OSCClient Listen Loop");
      CancellationToken cancellationToken = (CancellationToken)obj;
      var packetsToProcess = new Queue<OscPacket>();

      while (!cancellationToken.IsCancellationRequested && _oscSocketState)
      {
        try
        {
          if (receiver.State != OscSocketState.Connected)
          {
            if (DateTime.UtcNow - _lastReconnectAttempt > TimeSpan.FromMilliseconds(RECONNECT_ATTEMPT_DELAY_MS))
            {
              AttemptReconnect();
            }
            continue;
          }

          while (receiver.TryReceive(out OscPacket packet))
          {
            packetsToProcess.Enqueue(packet);
          }

          while (packetsToProcess.Count > 0)
          {
            var packet = packetsToProcess.Dequeue();
            if (packet is OscBundle bundle)
            {
              foreach (var message in bundle)
              {
                ProcessOscMessage(message as OscMessage);
              }
            }
          }

          Thread.Sleep(1);
        }
        catch (Exception ex)
        {
          UniLog.Log($"[VRCFTReceiver] Error in OSCClient ListenLoop: {ex.Message}");
          Thread.Sleep(100);
        }
      }

      UniLog.Log("[VRCFTReceiver] OSCClient ListenLoop ended");
    }

    private void AttemptReconnect()
    {
      try
      {
        _lastReconnectAttempt = DateTime.UtcNow;
        receiver?.Close();
        receiver = new OscReceiver(_ip, _port);
        receiver.Connect();
        UniLog.Log("[VRCFTReceiver] Successfully reconnected OSC receiver");
      }
      catch (Exception ex)
      {
        UniLog.Error($"[VRCFTReceiver] Failed to reconnect OSC receiver: {ex.Message}");
      }
    }

    private void ProcessOscMessage(OscMessage message)
    {
      if (message == null)
      {
        UniLog.Log("[VRCFTReceiver] null message");
        return;
      }

      var index = Expressions.GetIndex(message.Address);
      if (index == ExpressionIndex.Count)
      {
        UniLog.Log($"[VRCFTReceiver] unknown address {message.Address}");
        return;
      }

      _ftData[(int)index] = (float)message[0];

      if (message.Address.StartsWith(EYE_PREFIX))
      {
        LastEyeTracking = DateTime.UtcNow;
      }
      else if (message.Address.StartsWith(MOUTH_PREFIX))
      {
        LastFaceTracking = DateTime.UtcNow;
      }
    }

    public static void SendMessage(IPAddress ipAddress, int port, string address, string value)
    {
      try
      {
        using (var sender = new OscSender(ipAddress, port))
        {
          sender.Connect();
          sender.Send(new OscMessage(address, value));
        }
        UniLog.Log($"[VRCFTReceiver] Sent OSC message to {ipAddress}:{port} - Address: {address}, Value: {value}");
      }
      catch (Exception ex)
      {
        UniLog.Log($"[VRCFTReceiver] Error sending OSC message: {ex.Message}");
      }
    }

    public void Teardown()
    {
      UniLog.Log("[VRCFTReceiver] OSCClient teardown called");
      LastEyeTracking = null;
      LastFaceTracking = null;
      _oscSocketState = false;
      cancellationTokenSource?.Cancel();
      receiver?.Close();
      receiveThread?.Join(TimeSpan.FromSeconds(5));
      UniLog.Log("[VRCFTReceiver] OSCClient teardown completed");
    }
  }
}
