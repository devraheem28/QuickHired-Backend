
using QuickHired.BOL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BLL
{
    /// <summary>
    /// Represents the business logic operations for Meetings.
    /// </summary>
    public interface IMeetingsBs
    {
        /// <summary>
        /// Retrieves all meetings from the database.
        /// </summary>
        /// <returns>A collection of meetings.</returns>
        IEnumerable<Meetings> GetAll();

        /// <summary>
        /// Retrieves a meeting by its ID.
        /// </summary>
        /// <param name="id">The ID of the meeting.</param>
        /// <returns>The meeting object if found; otherwise, null.</returns>
        Meetings GetById(int id);

        /// <summary>
        /// Inserts a new meeting into the database.
        /// </summary>
        /// <param name="meeting">The meeting to insert.</param>
        /// <returns>The inserted meeting object.</returns>
        Meetings Insert(Meetings meeting);

        /// <summary>
        /// Updates an existing meeting in the database.
        /// </summary>
        /// <param name="meeting">The meeting to update.</param>
        /// <returns>The updated meeting object.</returns>
        Meetings Update(Meetings meeting);

        /// <summary>
        /// Deletes a meeting from the database.
        /// </summary>
        /// <param name="id">The ID of the meeting to delete.</param>
        /// <returns>True if the deletion was successful; otherwise, false.</returns>
        bool Delete(int id);
    }
    //public class MeetingsBs : IMeetingsBs
    //{
    //    private readonly IMeetingsDb _meetingsDb;
    //    public MeetingsBs(IMeetingsDb meetingsDb)
    //    {
    //        _meetingsDb = meetingsDb;
    //    }

    //    public bool Delete(int id)
    //    {
    //        return _meetingsDb.Delete(id);
    //    }

    //    public IEnumerable<Meetings> GetAll()
    //    {
    //        return _meetingsDb.GetAll();
    //    }

    //    public Meetings GetById(int id)
    //    {
    //        return _meetingsDb.GetById(id);
    //    }

    //    public Meetings Insert(Meetings meeting)
    //    {
    //        return _meetingsDb.Insert(meeting);
    //    }

    //    public Meetings Update(Meetings meeting)
    //    {
    //        return _meetingsDb.Update(meeting);
    //    }
    //}
}
