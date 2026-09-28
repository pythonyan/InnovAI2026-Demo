namespace InnovAI.Demo.Api.DTO;

public class CrmDataResponse
{
    public int Status { get; set; }
    public List<CrmTicket>? Data { get; set; }
}

public class CrmTicket
{
    public string? Id { get; set; }
    public string? Ticket_No { get; set; }
    public string? Ticket_Title { get; set; }
    public string? Description { get; set; }
    public string? Solution { get; set; }
    public string? TicketStatus { get; set; }
    public string? CreatedTime { get; set; }
    public string? ModifiedTime { get; set; }
    public string? Email_From { get; set; }
    public string? Email_To { get; set; }
    public string? Mailscanner_Action { get; set; }
}
