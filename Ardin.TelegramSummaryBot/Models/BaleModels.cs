public class BaleResponse
{
    public bool ok { get; set; }
    public List<UpdateResponse> result { get; set; }
}
public class UpdateResponse
{
    public long update_id { get; set; }
    public BaleMessage message { get; set; }
}
public class BaleMessage
{
    public long message_id { get; set; }
    public BaleChat chat { get; set; }
    public string text { get; set; }
}
public class BaleChat
{
    public long id { get; set; }
}
