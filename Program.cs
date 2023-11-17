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
            port.Open();
            while (true)
            {
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
            }
        }
    }

    static async Task SendToSensorApi(int serialData)
    {
        var payload = new
        {
            watertonId = 722,
            sensorID = 1,
            waarde = serialData,
            type = "waterniveau"
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await client.PostAsync(sensorApi, content);
    }

    static async Task ListenToValveApi(SerialPort port)
    {
        var response = await client.GetAsync(valveApi);
        var data = JsonSerializer.Deserialize<ValveData>(await response.Content.ReadAsStringAsync());
        if (data.open) {
            port.WriteLine("on");
        } else {
            port.WriteLine("off");
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