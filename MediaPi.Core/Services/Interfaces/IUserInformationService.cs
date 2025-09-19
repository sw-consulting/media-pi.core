// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Mvc;

namespace MediaPi.Core.Services.Interfaces
{
    public interface IUserInformationService
    {
        Task<bool> CheckAdmin(int cuid);
        Task<bool> CheckManager(int cuid, int accountId);
        Task<bool> CheckAdminOrSameUser(int id, int cuid);
        bool CheckSameUser(int id, int cuid);
        bool Exists(int id);
        bool Exists(string email);
        Task<UserViewItem?> UserViewItem(int id);
        Task<List<UserViewItem>> UserViewItems();
        public List<int> GetUserAccountIds(User user);
        public bool ManagerOwnsAccount(User user, Account account);
        public bool ManagerOwnsGroup(User user, DeviceGroup group);
        public bool ManagerOwnsDevice(User user, Device device);
        public bool UserCanManageDeviceServices(User user, Device device);
        public bool UserCanViewDevice(User user, Device device);
        public bool UserCanAssignGroup(User user, Device device);

    }
}
