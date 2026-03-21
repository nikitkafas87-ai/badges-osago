using System.IO;
using System.Text.Json;

namespace OsagoSeleniumTests.Config
{
    public class TestConfig
    {
        public string BaseUrl { get; private set; }
        public string LicensePlate { get; private set; }
        public string CarBrand { get; private set; }
        public string CarModel { get; private set; }
        public string CarYear { get; private set; }
        public string CarPower { get; private set; }
        public string StsNumber { get; private set; }
        public string StsDate { get; private set; }
        public string VinNumber { get; private set; }
        public string OwnerLastName { get; private set; }
        public string OwnerFirstName { get; private set; }
        public string OwnerMiddleName { get; private set; }
        public string OwnerBirthDate { get; private set; }
        public string PassportNumber { get; private set; }
        public string PassportDate { get; private set; }
        public string OwnerAddress { get; private set; }
        public string ApartmentNumber { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }

        private TestConfig(
            string baseUrl, string licensePlate, string carBrand, string carModel,
            string carYear, string carPower, string stsNumber, string stsDate, string vinNumber,
            string ownerLastName, string ownerFirstName, string ownerMiddleName,
            string ownerBirthDate, string passportNumber, string passportDate,
            string ownerAddress, string apartmentNumber, string email, string phone)
        {
            BaseUrl = baseUrl;
            LicensePlate = licensePlate;
            CarBrand = carBrand;
            CarModel = carModel;
            CarYear = carYear;
            CarPower = carPower;
            StsNumber = stsNumber;
            StsDate = stsDate;
            VinNumber = vinNumber;
            OwnerLastName = ownerLastName;
            OwnerFirstName = ownerFirstName;
            OwnerMiddleName = ownerMiddleName;
            OwnerBirthDate = ownerBirthDate;
            PassportNumber = passportNumber;
            PassportDate = passportDate;
            OwnerAddress = ownerAddress;
            ApartmentNumber = apartmentNumber;
            Email = email;
            Phone = phone;
        }

        public static TestConfig Load()
        {
            var json = File.ReadAllText("appsettings.json");
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var env = root.GetProperty("Environment").GetString() ?? "Test";
            var envNode = root.GetProperty("Environments").GetProperty(env);

            return new TestConfig(
                envNode.GetProperty("BaseUrl").GetString()!,
                envNode.GetProperty("LicensePlate").GetString()!,
                envNode.GetProperty("CarBrand").GetString()!,
                envNode.GetProperty("CarModel").GetString()!,
                envNode.GetProperty("CarYear").GetString()!,
                envNode.GetProperty("CarPower").GetString()!,
                envNode.GetProperty("StsNumber").GetString()!,
                envNode.GetProperty("StsDate").GetString()!,
                envNode.GetProperty("VinNumber").GetString()!,
                envNode.GetProperty("OwnerLastName").GetString()!,
                envNode.GetProperty("OwnerFirstName").GetString()!,
                envNode.GetProperty("OwnerMiddleName").GetString()!,
                envNode.GetProperty("OwnerBirthDate").GetString()!,
                envNode.GetProperty("PassportNumber").GetString()!,
                envNode.GetProperty("PassportDate").GetString()!,
                envNode.GetProperty("OwnerAddress").GetString()!,
                envNode.GetProperty("ApartmentNumber").GetString()!,
                envNode.GetProperty("Email").GetString()!,
                envNode.GetProperty("Phone").GetString()!
            );
        }
    }
}
