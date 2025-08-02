// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Services
{
    public class UserInformationService : IUserInformationService
    {
        private readonly AppDbContext _context;

        public UserInformationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CheckAdmin(int cuid)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(x => x.Id == cuid)
                .FirstOrDefaultAsync();
            return user != null && user.IsAdministrator();
        }

        public async Task<bool> CheckManager(int cuid)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(x => x.Id == cuid)
                .FirstOrDefaultAsync();
            return user != null && user.IsManager();
        }

        public async Task<ActionResult<bool>> CheckAdminOrSameUser(int id, int cuid)
        {
            if (cuid == 0) return false;
            if (cuid == id) return true;
            return await CheckAdmin(cuid);
        }

        public bool CheckSameUser(int id, int cuid)
        {
            if (cuid == 0) return false;
            if (cuid == id) return true;
            return false;
        }

        public bool Exists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        public bool Exists(string email)
        {
            return _context.Users.Any(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<UserViewItem?> UserViewItem(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(x => x.Id == id)
                .Select(x => new UserViewItem(x))
                .FirstOrDefaultAsync();
            return user ?? null;
        }

        public async Task<List<UserViewItem>> UserViewItems()
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Select(x => new UserViewItem(x))
                .ToListAsync();
        }
    }
}