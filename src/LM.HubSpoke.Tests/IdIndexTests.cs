using LM.HubSpoke.Entries;
using LM.Infrastructure.Text;
using Xunit;

public class IdIndexTests
{
    [Fact]
    public void AddAndFind_Works_For_Doi_And_Pmid()
    {
        var doi = new DoiNormalizer();
        var pmid = new PmidNormalizer();
        var idx = new IdIndex(doi.Normalize, pmid.Normalize);

        idx.AddOrUpdate("doi:10.1056/NEJMoa1514616", "PMID: 12345678", "id-1");
        idx.AddOrUpdate("10.1001/jamacardio.2022.2695", null, "id-2");

        Assert.Equal("id-1", idx.Find("10.1056/nejmoa1514616", null));
        Assert.Equal("id-1", idx.Find(null, "12345678"));
        Assert.Equal("id-2", idx.Find("10.1001/jamacardio.2022.2695", null));
        Assert.Null(idx.Find("10.9999/does.not.exist", null));
    }
}
