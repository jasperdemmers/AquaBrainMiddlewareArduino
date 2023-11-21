using System;
using System.IO.Ports;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetEnv;

class Program {
    static readonly HttpClient client = new HttpClient();
    static string username;
    static string password;
    static bool previousState = false;
    static readonly string sensorApi = "http://192.168.154.23/api/Sensor/data";
    static readonly string valveApi = "http://192.168.154.23/api/Valve/722/1";

    static async Task Main(string[] args)
    {
        Env.Load();
        username = Env.GetString("username");
        password = Env.GetString("password");

        var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        using (SerialPort port = new SerialPort("/dev/cu.usbserial-2130"))
        {
            port.ReadTimeout = 5000;
            port.BaudRate = 9600;
            try
            {
                port.Open();
                while (true)
                {
                    try {
                        string serialData = port.ReadLine();
                        if (int.TryParse(serialData, out int serialNumber))
                        {
                            await SendToSensorApi(serialNumber);
                            await ListenToValveApi(port);
                        }
                        else
                        {
                            Console.WriteLine($"Invalid data received: {serialData}");
                        }
                    } catch (TimeoutException) {
                        Console.WriteLine("Read from serial port timed out.");
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine($"Serial port disconnected: {e.Message}");
                if (port.IsOpen)
                {
                    port.Close();
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine($"Access to serial port denied: {e.Message}");
                if (port.IsOpen)
                {
                    port.Close();
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }

    static async Task SendToSensorApi(int serialData)
    {
        try {
            var payload = new
            {
                watertonId = 722,
                sensorID = 1,
                waarde = serialData,
                type = "waterniveau"
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await client.PostAsync(sensorApi, content);
        } catch (Exception e) {
            Console.WriteLine($"Error in SendToSensorApi: {e.Message}");
        }
    }

    static async Task ListenToValveApi(SerialPort port)
    {
        try {
            var response = await client.GetAsync(valveApi);
            var data = JsonSerializer.Deserialize<ValveData>(await response.Content.ReadAsStringAsync());
            if (data.open != previousState)
            {
                if (data.open) {
                    port.WriteLine("on");
                    Console.WriteLine("Valve opened");
                } else {
                    port.WriteLine("off");
                    Console.WriteLine("Valve closed");
                }
                previousState = data.open;
            }
        } catch (Exception e) {
            Console.WriteLine($"Error in ListenToValveApi: {e.Message}");
        }
    }
}

public class ValveData
{
    public int id { get; set; }
    public bool open { get; set; }
    public int watertonId { get; set; }
    public string createdDate { get; set; }
    public object waterton { get; set; }
}