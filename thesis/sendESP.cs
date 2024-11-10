using System;
using System.IO.Ports;

public class sendESP
{
    SerialPort port;
    private bool state = false;
    public sendESP(int selectPort = 0, int baudRate = 115200)
    {
        string[] ports = SerialPort.GetPortNames();

        if (ports.Length > 0)
        {
            if (selectPort < ports.Length)
            {
                port = new SerialPort(ports[selectPort], baudRate, Parity.None, 8, StopBits.One);
                state = true;
            }
            else
            {
                Console.WriteLine("serial port is invalid");
            }
        }
        else
        {
            Console.WriteLine("No serial ports found.");
        }
    }
    public void send()
    {
        if (!state) return;

    }
}


