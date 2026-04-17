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

        public DateTime? ReviewedAt { get; set; }

        public RequestUserDTO User { get; set; }

        public RequestPetDTO Pet { get; set; }

        public RequestStatusDTO Status { get; set; }
    }

    public class RequestUserDTO
    {
        public string FullName { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }
    }

    public class RequestPetDTO
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Species { get; set; }

        public string Breed { get; set; }

        public int AgeYears { get; set; }

        public List<RequestPetImageDTO> Images { get; set; }
    }

    public class RequestPetImageDTO
    {
        public string ImageUrl { get; set; }

        public bool IsPrimary { get; set; }
    }

    public class RequestStatusDTO
    {
        public string Name { get; set; }
    }
}