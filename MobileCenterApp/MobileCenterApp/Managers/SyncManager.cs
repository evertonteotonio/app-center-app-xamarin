﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace MobileCenterApp
{
	public class SyncManager
	{
		public static SyncManager Shared { get; set; } = new SyncManager();
		public MobileCenterApi.MobileCenterAPIServiceApiKeyApi Api { get; set; } = new MobileCenterApi.MobileCenterAPIServiceApiKeyApi("MobileCenter", "Ttw8AMUjYeEkr==");

		Task syncAppsTask;
		public Task SyncApps()
		{
			if (syncAppsTask?.IsCompleted ?? true)
				syncAppsTask = syncApps();
			return syncAppsTask;
		}

		async Task syncApps()
		{
			if (Settings.IsOfflineMode)
				return;
			var apps = await Api.GetApps();
			List<Owner> owners = new List<Owner>();
			List<AppClass> myApps = new List<AppClass>();

			apps.ToList().ForEach(x =>
			{
				myApps.Add((AppClass)x.ToAppClass());
				owners.Add((Owner)x.Owner.ToAppOwner());
			});

			await Database.Main.ResetTable<AppClass>();
			await Database.Main.InsertAllAsync(myApps);
			var distintOwners = owners.DistinctBy(x => x.Id).ToList();
			Database.Main.InsertOrReplaceAll(distintOwners);
			NotificationManager.Shared.ProcAppsChanged();
		}

		Task<User> userTask;
		public Task<User> GetUser()
		{
			if (userTask?.IsCompleted ?? true)
				userTask = Task.Run(async () =>
				{
					var profile = await Api.GetUserProfile();
					var user = new User
					{
						AvatarUrl = profile.AvatarUrl,
						DisplayName = profile.DisplayName,
						CanChangePassword = profile.CanChangePassword,
						Email = profile.Email,
						Id = profile.Id,
						Name = profile.Name,
					};
					Settings.CurrentUser = user;
					return user;
				});
			return userTask;
		}

		public async Task<bool> CreateApp(AppClass app)
		{
			try
			{
				var resp = await Api.PostCreateApp(app.ToAppRequest());
				var newApp = resp.ToAppClass();
				var owner = resp.Owner.ToAppOwner();
				Database.Main.InsertOrReplace(newApp);
				Database.Main.InsertOrReplace(owner);
				Database.Main.ClearMemory<AppClass>();
				NotificationManager.Shared.ProcAppsChanged();
				return true;
			}
			catch (Exception ex)
			{
				if (ex.Data.Contains("HttpContent"))
				{
					Console.WriteLine(ex.Data["HttpContent"]);
				}
				Console.WriteLine(ex);
			}
			return false;
		}
	}
}
