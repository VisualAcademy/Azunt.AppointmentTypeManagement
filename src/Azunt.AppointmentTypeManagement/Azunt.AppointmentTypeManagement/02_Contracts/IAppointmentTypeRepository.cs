namespace Azunt.AppointmentTypeManagement
{
    public interface IAppointmentTypeRepository
    {
        Task AddAsync(AppointmentType appointmentType, string connectionString);
        Task<List<AppointmentType>> GetAllAsync(string connectionString);
        Task<AppointmentType> GetByIdAsync(long id, string connectionString);
        Task UpdateAsync(AppointmentType appointmentType, string connectionString);
        Task DeleteAsync(long id, string connectionString);
    }
}
