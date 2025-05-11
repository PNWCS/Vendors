public class Vendor
{
    public string Name { get; set; }
    public string Fax { get; set; }
    public string QB_ID { get; set; }
    public string Company_ID { get; set; }
    public VendorStatus Status { get; set; }

    public Vendor(string name, string fax)
    {
        Name = name;
        Fax = fax;
    }

    public Vendor(string name, string fax, string companyId)
    {
        Name = name;
        Fax = fax;
        Company_ID = companyId;
    }

    public override string ToString()
    {
        return $"Name: {Name}, Fax: {Fax}, QB_ID: {QB_ID}, Company_ID: {Company_ID}, Status: {Status}";
    }
}

public enum VendorStatus
{
    Added,
    Missing,
    Different,
    Unchanged
}