using System;
using System.IO;
using BestoNet.Collections;
using Godot;

namespace IdolShowdown.Managers
{
    public partial class MatchMessageManager : Node
    {
        public static MatchMessageManager Instance { get; private set; }

        //[Export] public int MAX_RETRY_AMOUNT = 3;
        //[Export] public int MATCH_MESSAGE_CHANNEL = 4;
        public int Ping { get; private set; } = 200;
        private const bool PACKET_ACK = true;
        public CircularArray<float> sentFrameTimes = new CircularArray<float>(60);

        /* Global manager references */
        private RollbackManager rollbackManager => RollbackManager.Instance;
        //private LobbyManager lobbyManager => GlobalManager.Instance.LobbyManager;

        public override void _Ready()
        {
            Instance = this;
        }

        /*
        void Update()
        {
            if (lobbyManager != null && lobbyManager.CurrentLobby != null && lobbyManager.LobbyMemberMe.userRank != PlayerLobbyType.spectator)
            {
                while (SteamNetworking.IsP2PPacketAvailable(MATCH_MESSAGE_CHANNEL))
                {
                    P2Packet? lastPacket = SteamNetworking.ReadP2PPacket(MATCH_MESSAGE_CHANNEL);
                    if (lastPacket.HasValue && lastPacket.Value.Data != null)
                    {
                        try
                        {
                            OnChatMessage(lastPacket.Value.SteamId.Value, lastPacket.Value.Data);
                        }
                        catch (System.Exception e)
                        {
                            GD.PrintErr(e);
                        }
                    }
                }
            }
        }
        */

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferChannel = 0, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        private void OnChatMessage(byte[] message)
        {
            /*
            if (GlobalManager.Instance.OnlineComponents.rematchHelper.IsActivated || !GlobalManager.Instance.MatchRunner.IsRunning)
            {
                return;
            }
            */
            long fromUser = Multiplayer.GetRemoteSenderId();

            MemoryStream memoryStream = new MemoryStream(message);
            BinaryReader reader = new BinaryReader(memoryStream);

            bool PACKET_ACK = reader.ReadBoolean();
            if (PACKET_ACK)
            {
                int frame = reader.ReadInt32();
                ProcessACK(frame);
            }
            else
            {
                int remoteFrameAdvantage = reader.ReadInt32();  
                int totalInputs = reader.ReadInt32();
                for(int i = 0; i <= totalInputs; i++)
                {
                    int frame = reader.ReadInt32();
                    ulong input = reader.ReadUInt64();
                    if (i == totalInputs)
                    {
                        rollbackManager.SetRemoteFrameAdvantage(frame, remoteFrameAdvantage);
                        rollbackManager.SetRemoteFrame(frame);
                    }
                    ProcessInputs(frame, input, fromUser);
                    
                }             
            }
            
            reader.Close();
            memoryStream.Close();
        }

        public void ProcessInputs(int frame, ulong input, long fromUser)
        {
            if (rollbackManager.receivedInputs.ContainsKey(frame))
            {
                return;
            }
            SendMessageACK(fromUser, frame);
            rollbackManager.SetOpponentInput(frame, input);
        }

        public void ProcessACK(int frame)
        {
            //do this weird conversion so that the code is as accurate (output wise) to the original
            float time = (float)Time.GetTicksMsec() / 1000.0f;
            int CalculatePing = (int)((time - sentFrameTimes.Get(frame)) * 1000);
            Ping = CalculatePing == 0 ? Ping : CalculatePing;
        }

        public void SendInputs(long userid, int frame, ulong input)
        {
            /*
            if (GlobalManager.Instance.OnlineComponents.rematchHelper.IsActivated)
            {
                return;
            }
            */

            if (!rollbackManager.clientInputs.ContainsKey(frame))
            {
                rollbackManager.SetClientInput(frame, input);
            }
            
            sentFrameTimes.Insert(frame, (float)Time.GetTicksMsec() / 1000.0f);
            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream);

            binaryWriter.Write(!PACKET_ACK);
            binaryWriter.Write(rollbackManager.localFrameAdvantage);
            binaryWriter.Write(Math.Min(rollbackManager.MaxRollBackFrames, frame));
            for(int i = Math.Max(frame - rollbackManager.MaxRollBackFrames, 0); i <= frame; i++)
            {
                binaryWriter.Write(rollbackManager.clientInputs.Get(i).frame);
                binaryWriter.Write(rollbackManager.clientInputs.Get(i).input);
            }

            byte[] data = memoryStream.ToArray();
            //SteamNetworking.SendP2PPacket(userid, data, data.Length, MATCH_MESSAGE_CHANNEL, P2PSend.UnreliableNoDelay);
            RpcId(userid, MethodName.OnChatMessage, data);

            binaryWriter?.Dispose();
            binaryWriter?.Close();
            memoryStream?.Dispose();
            memoryStream?.Close();
        }

        public void SendMessageACK(long userid, int frame)
        {
            /*
            if (GlobalManager.Instance.OnlineComponents.rematchHelper.IsActivated)
            {
                return;
            }
            */

            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream);

            binaryWriter.Write(PACKET_ACK);
            binaryWriter.Write(frame);

            byte[] data = memoryStream.ToArray();
            //SteamNetworking.SendP2PPacket(userid, data, data.Length, MATCH_MESSAGE_CHANNEL, P2PSend.UnreliableNoDelay);
            RpcId(userid, MethodName.OnChatMessage, data);

            binaryWriter?.Dispose();
            binaryWriter?.Close();
            memoryStream?.Dispose();
            memoryStream?.Close();
                
        }
    }
}