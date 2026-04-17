namespace PA_BACKEND.DTOs
{
    public class AdoptionRequestDTO
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int PetId { get; set; }
        public int StatusId { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}