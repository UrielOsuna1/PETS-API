namespace PA_BACKEND.DTOs
{
    public class AdoptionRequestCreateDTO
    {
        public int UserId { get; set; }
        public int PetId { get; set; }
        public string Message { get; set; }
    }
}