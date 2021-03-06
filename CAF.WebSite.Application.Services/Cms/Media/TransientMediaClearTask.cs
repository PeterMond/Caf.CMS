﻿using CAF.Infrastructure.Core.Data;
using CAF.WebSite.Application.Services.Tasks;
using CAF.Infrastructure.Core.Domain.Cms.Media;
using System;
using System.Linq;
using System.Linq.Expressions;
 

namespace CAF.WebSite.Application.Services.Media
{
    /// <summary>
    /// Represents a task for deleting transient media from the database
	/// (pictures and downloads which have been uploaded but never assigned to an entity)
    /// </summary>
    public partial class TransientMediaClearTask : ITask
    {
		private readonly IPictureService _pictureService;
		private readonly IRepository<Picture> _pictureRepository;
		private readonly IRepository<Download> _downloadRepository;

		public TransientMediaClearTask(
			IPictureService pictureService,
			IRepository<Picture> pictureRepository, 
			IRepository<Download> downloadRepository)
        {
			this._pictureService = pictureService;
			this._pictureRepository = pictureRepository;
			this._downloadRepository = downloadRepository;
        }

		public void Execute(TaskExecutionContext ctx)
        {
			// delete all media records which are in transient state since at least 3 hours
			var olderThan = DateTime.UtcNow.AddHours(-3);

			// delete Downloads
            _downloadRepository.DeleteAll(x => x.IsTransient && x.UpdatedOnUtc < olderThan);

			// delete Pictures
			var autoCommit = _pictureRepository.AutoCommitEnabled;
			_pictureRepository.AutoCommitEnabled = false;

			try
			{
				using (var scope = new DbContextScope(autoDetectChanges: false, validateOnSave: false, hooksEnabled: false))
				{
                    var pictures = _pictureRepository.Where(x => x.IsTransient && x.UpdatedOnUtc < olderThan).ToList();
					foreach (var picture in pictures)
					{
						_pictureService.DeletePicture(picture);
					}

					_pictureRepository.Context.SaveChanges();

					if (DataSettings.Current.IsSqlServer)
					{
						try
						{
							_pictureRepository.Context.ExecuteSqlCommand("DBCC SHRINKDATABASE(0)", true);
						}
						catch { }
					}
				}
			}
			finally
			{
				_pictureRepository.AutoCommitEnabled = autoCommit;
			}
        }
    }
}
