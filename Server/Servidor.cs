using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    //Enumerador com comandos para interação entre servidor e cliente
    enum Command
    {
        Login,      //Entrada/conectar
        Logout,     //Saída/desconectar
        Message,    //Envio de mensagem para todos os clientes
        List,       //Obter lista dos utilizadores
        Null,        //auxiliar
        Prog,
        Paint
    }

    public partial class Servidor : Form
    {
        //Estrutura com informação de todos os clientes ligados ao servidor
        struct ClientInfo
        {
            public EndPoint endpoint;   //Socket para o cliente
            public string strName;      //Nome do cliente no Chat
        }

        //Colecção de todos os clientes no Chat(array do tipo ClientInfo)
        ArrayList clientList;
        public int progBar = 100;
        //Socket principal que aguarda conexões
        Socket serverSocket;

        byte[] byteData = new byte[1024];

        public Servidor()
        {
            clientList = new ArrayList();
            InitializeComponent();
        }

    private void Form1_Load(object sender, EventArgs e)
    {            
        try
        {
	    CheckForIllegalCrossThreadCalls = false;

            //Tipo de socket -> UDP
            serverSocket = new Socket(AddressFamily.InterNetwork, 
                SocketType.Dgram, ProtocolType.Udp);

            //IP do servidor a aguardar ligação na porta 1000
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1000);

            //Associar o IP ao Socket
            serverSocket.Bind(ipEndPoint);
            
            IPEndPoint ipeSender = new IPEndPoint(IPAddress.Any, 0);
            //Identificar clientes 
            EndPoint epSender = (EndPoint) ipeSender;

             
            //Receber dados
            serverSocket.BeginReceiveFrom (byteData, 0, byteData.Length, 
                SocketFlags.None, ref epSender, new AsyncCallback(OnReceive), epSender);                
        }
        catch (Exception ex) 
        { 
            MessageBox.Show(ex.Message, "SGSServerUDP", 
                MessageBoxButtons.OK, MessageBoxIcon.Error); 
        }            
    }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                IPEndPoint ipeSender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint epSender = (EndPoint)ipeSender;

                serverSocket.EndReceiveFrom (ar, ref epSender);
                
                //Transformar o array de bytes recebido do utilizador num objecto de dados
                Data msgReceived = new Data(byteData);

                //Enviar o objecto em resposta aos pedidos dos clientes
                Data msgToSend = new Data();

                byte [] message;
                
                //Controlar o tipo de mensagem (login, logout, ou apenas texto)
                msgToSend.cmdCommand = msgReceived.cmdCommand;
                msgToSend.strName = msgReceived.strName;
                switch (msgReceived.cmdCommand)
                {
                    case Command.Login:

                        //Quando um cliente se liga, é adicionado à lista de 
                        ClientInfo clientInfo = new ClientInfo();
                        clientInfo.endpoint = epSender;      
                        clientInfo.strName = msgReceived.strName;                        

                        clientList.Add(clientInfo);
                        //Mensagem que vai ser enviada para todos os utilizadores   
                        break;

                    case Command.Logout:                    
                        
                        //Quando um cliente se quer desconectar, faz-se a pesquisa do mesmo na lista e termina a ligação correspondente

                        int nIndex = 0;
                        foreach (ClientInfo client in clientList)
                        {
                            if (client.endpoint == epSender)
                            {
                                clientList.RemoveAt(nIndex);
                                break;
                            }
                            ++nIndex;
                        }                                               
                        
                        msgToSend.strMessage = msgReceived.strName + " saiu!";
                        break;

                    case Command.Message:

                        //Mensagem que vai ser enviada para todos os utilizadores
                        msgToSend.strMessage = msgReceived.strName + ": " + msgReceived.strMessage;
                        break;

                    case Command.List:

                        //Enviar a lista de utilizadores ao novo cliente
                        msgToSend.cmdCommand = Command.List;
                        msgToSend.strName = null;
                        msgToSend.strMessage = null;

                        //Colecção de utilizadores no chat
                        foreach (ClientInfo client in clientList)
                        {
                            //utiliza-se o símbolo (   *   ) para separar os nomes
                            msgToSend.strMessage += client.strName + "*";   
                        }                        

                        message = msgToSend.ToByte();

                        //Enviar o nome dos utilizadores no chat
                        serverSocket.BeginSendTo (message, 0, message.Length, SocketFlags.None, epSender, 
                                new AsyncCallback(OnSend), epSender);                        
                        break;
                }

                if (msgToSend.cmdCommand != Command.List)
                {
                    message = msgToSend.ToByte();

                    foreach (ClientInfo clientInfo in clientList)
                    {
                        if (clientInfo.endpoint != epSender ||
                            msgToSend.cmdCommand != Command.Login)
                        {
                            //Enviar mensagem a todos os clientes
                            serverSocket.BeginSendTo (message, 0, message.Length, SocketFlags.None, clientInfo.endpoint, 
                                new AsyncCallback(OnSend), clientInfo.endpoint);                           
                        }
                    }

                    txtLog.Text += msgToSend.strMessage + "\r\n";
                }

                //Se o utilizador saiu, não é necessário continuar a aguardar dados
                if (msgReceived.cmdCommand != Command.Logout)
                {
                    //Aguardar dados do cliente
                    serverSocket.BeginReceiveFrom (byteData, 0, byteData.Length, SocketFlags.None, ref epSender, 
                        new AsyncCallback(OnReceive), epSender);
                }
                if (clientList.Count > 0)
                {
                    timerJogo_Ronda.Start();
                    timerProgBar.Start();
                }
                else
                {

                    timerJogo_Ronda.Stop();
                    timerProgBar.Stop();
                }
            }
            catch (Exception ex)
            { 
                MessageBox.Show(ex.Message, "Servidor", MessageBoxButtons.OK, MessageBoxIcon.Error); 
            }
        }
        public void Joga()
        {
            int jogador;
            Data msgReceived = new Data(byteData);
            Data msgToSend = new Data();

            byte[] message;
            do
            {
                Random rdn = new Random();
                jogador = rdn.Next(clientList.Count);
            }
            while (clientList[jogador] == null);

            for(int idx = 0; idx < clientList.Count; idx++)
            {
                ClientInfo clientInfo = (ClientInfo)clientList[idx];
                msgToSend.cmdCommand = Command.Null;
                msgToSend.strName = clientInfo.strName;
                if (idx == jogador)
                    msgToSend.strMessage = "Joga";
                else
                    msgToSend.strMessage = "Não Joga";
                txtLog.Text += msgToSend.strMessage + " " + msgToSend.strName + "\r\n";
                message = msgToSend.ToByte();
                serverSocket.BeginSendTo(message, 0, message.Length, SocketFlags.None, clientInfo.endpoint,
                                new AsyncCallback(OnSend), clientInfo.endpoint);
            }
        }
        public void progressBar()
        {
            Data msgReceived = new Data(byteData);
            Data msgToSend = new Data();

            byte[] message;
            msgToSend.strMessage = progBar.ToString();
            msgToSend.cmdCommand = Command.Prog;
            message = msgToSend.ToByte();
            foreach(ClientInfo clientInfo in clientList)
            {
                serverSocket.BeginSendTo(message, 0, message.Length, SocketFlags.None, clientInfo.endpoint,
                            new AsyncCallback(OnSend), clientInfo.endpoint);
            }
            if (progBar > 0)
                progBar -= 5;
            else
                progBar = 100;
        }
        public void OnSend(IAsyncResult ar)
        {
            try
            {                
                serverSocket.EndSend(ar);
            }
            catch (Exception ex)
            { 
                MessageBox.Show(ex.Message, "Servidor", MessageBoxButtons.OK, MessageBoxIcon.Error); 
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if(clientList.Count > 0)
                Joga();
        }

        private void timerProgBar_Tick(object sender, EventArgs e)
        {
            if (clientList.Count > 0)
                progressBar();
        }

        private void Send_Information_Tick(object sender, EventArgs e)
        {

        }
    }

    //Estrutura de dados para servidores e clientes poderem comunicar
    class Data
    {
        public Data()
        {
            this.cmdCommand = Command.Null;
            this.strMessage = null;
            this.strName = null;
        }

        //Converte os bytes num objecto do tipo Data
        public Data(byte[] data)
        {
            //4 bytes para o comando
            this.cmdCommand = (Command)BitConverter.ToInt32(data, 0);

            //5-8 segundos bytes para o nome
            int nameLen = BitConverter.ToInt32(data, 4);

            //9-12 para a mensagem
            int msgLen = BitConverter.ToInt32(data, 8);

            //Garantir que a string strName passou para o array de bytes
            if (nameLen > 0)
                this.strName = Encoding.UTF8.GetString(data, 12, nameLen);
            else
                this.strName = null;

            //Verificar se a mensagem tem conteúdo
            if (msgLen > 0)
                this.strMessage = Encoding.UTF8.GetString(data, 12 + nameLen, msgLen);
            else
                this.strMessage = null;
        }

        //Converter a estrutura de dados num array de bytes
        public byte[] ToByte()
        {
            List<byte> result = new List<byte>();

            //primeiros 4 bytes para o comando
            result.AddRange(BitConverter.GetBytes((int)cmdCommand));

            //adicionar o nome
            if (strName != null)
                result.AddRange(BitConverter.GetBytes(strName.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //adicionar mensagem
            if (strMessage != null)
                result.AddRange(BitConverter.GetBytes(strMessage.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            if (strName != null)
                result.AddRange(Encoding.UTF8.GetBytes(strName));

            //adicionar a mensagem
            if (strMessage != null)
                result.AddRange(Encoding.UTF8.GetBytes(strMessage));

            return result.ToArray();
        }

        public string strName;      //Nome do cliente no Chat
        public string strMessage;   //Messagem
        public Command cmdCommand;  //Tipo de comando (login, logout, send message, ...)
    }
}