using dy.net.extension;
using dy.net.model.dto;
using dy.net.model.entity;
using dy.net.utils;
using SqlSugar;

namespace dy.net.repository
{
    public class DouyinVideoRepository : BaseRepository<DouyinVideo>
    {
        // 注入SQLSugar客户端
        public DouyinVideoRepository(ISqlSugarClient db) : base(db)
        {
        }




        public async Task<List<DouyinVideoTopDto>> GetTopsOrderBySyncTime(int top)
        {
            return await Db.Queryable<DouyinVideo>().Select(x => new DouyinVideoTopDto { Id = x.Id, Title = x.VideoTitle, Time = x.SyncTime.ToString("yyyy-MM-dd HH:mm:ss") }).Take(top).OrderByDescending(x => x.Time).ToListAsync();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<(List<DouyinVideo> list, int totalCount)> GetPagedAsync(DouyinVideoPageRequestDto dto)
        {
            DateTime? start, end;
            GetDateBetween(dto.Dates, out start, out end);

            DateTime? start2, end2;
            GetDateBetween(dto.Dates2, out start2, out end2);


            VideoTypeEnum? enumviedoType = null;
            if (!string.IsNullOrEmpty(dto.ViedoType) && dto.ViedoType != "*" && dto.ViedoType != "4")
            {
                enumviedoType = dto.ViedoType.ToVideoTypeEnum();
            }

            var where = this.Db.Queryable<DouyinVideo>()
                //.WhereIF(!string.IsNullOrWhiteSpace(title), x => x.VideoTitle.Contains(title))
                .WhereIF(!string.IsNullOrWhiteSpace(dto.Title), x => x.VideoTitle.Contains(dto.Title))
                .WhereIF(!string.IsNullOrWhiteSpace(dto.Author), x => x.Author.Contains(dto.Author))
                .WhereIF(!string.IsNullOrWhiteSpace(dto.Tag), x => x.Tag1 == dto.Tag)
                .WhereIF(start.HasValue, x => x.SyncTime >= start.Value)
                .WhereIF(end.HasValue, x => x.SyncTime <= end.Value)
                .WhereIF(start2.HasValue, x => x.CreateTime >= start2.Value)
                .WhereIF(end2.HasValue, x => x.CreateTime <= end2.Value)
                .WhereIF(enumviedoType.HasValue, x => x.ViedoType == enumviedoType)
                .WhereIF(!string.IsNullOrWhiteSpace(dto.CookieId),x=>x.CookieId==dto.CookieId)
                .WhereIF(dto.ViedoType == "4", x => x.IsMergeVideo == 1);


            var totalCount = await where.CountAsync();
            List<DouyinVideo> list = new List<DouyinVideo>();
            if (string.IsNullOrWhiteSpace(dto.SortField))
                list = await where.OrderByDescending(x => x.SyncTime).Skip((dto.PageIndex - 1) * dto.PageSize).Take(dto.PageSize).ToListAsync();
            else
            {

                list = await where.OrderBy($"{dto.SortField} {dto.SortOrder}").Skip((dto.PageIndex - 1) * dto.PageSize).Take(dto.PageSize).ToListAsync();
            }
            if (list.Any())
            {
                var users = await this.Db.Queryable<DouyinCookie>().ToListAsync();
                foreach (var item in list)
                {
                    var user = users.FirstOrDefault(x => x.Id == item.CookieId);
                    if (user != null)
                    {
                        item.DyUser = user.UserName;
                    }
                }
            }
            return (list, totalCount);
        }

        public async Task<(List<DouyinVideo> list, int totalCount)> GetAuthorVideosPagedAsync(
            string authorId,
            string authorName,
            int pageIndex,
            int pageSize,
            string title = null,
            string tag1 = null)
        {
            var query = BuildAuthorVideoQuery(authorId, authorName);
            if (query == null) return (new List<DouyinVideo>(), 0);

            query = query
                .WhereIF(!string.IsNullOrWhiteSpace(title), x => x.VideoTitle.Contains(title))
                .WhereIF(!string.IsNullOrWhiteSpace(tag1), x => x.Tag1 == tag1);

            var totalCount = await query.CountAsync();
            var list = await query
                .OrderBy(x => x.CreateTime, OrderByType.Desc)
                .OrderBy(x => x.SyncTime, OrderByType.Desc)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (list, totalCount);
        }

        public async Task<List<AuthorVideoTagDto>> GetAuthorVideoTagsAsync(string authorId, string authorName)
        {
            var query = BuildAuthorVideoQuery(authorId, authorName);
            if (query == null) return new List<AuthorVideoTagDto>();

            var grouped = await query
                .Where(x => x.Tag1 != null && x.Tag1 != "")
                .GroupBy(x => x.Tag1)
                .Select(x => new { Name = x.Tag1, Count = SqlFunc.AggregateCount(x.Id) })
                .ToListAsync();

            return grouped
                .Select(x => new AuthorVideoTagDto
                {
                    Name = x.Name,
                    Count = x.Count
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        private ISugarQueryable<DouyinVideo> BuildAuthorVideoQuery(string authorId, string authorName)
        {
            var query = Db.Queryable<DouyinVideo>();
            if (!string.IsNullOrWhiteSpace(authorId))
                query = query.Where(x => x.AuthorId == authorId);
            else if (!string.IsNullOrWhiteSpace(authorName))
                query = query.Where(x => x.Author == authorName);
            else
                return null;

            return query;
        }

        private static void GetDateBetween(List<string> dates, out DateTime? start, out DateTime? end)
        {
            start = null;
            end = null;
            if (dates != null && dates.Count == 2)
            {
                start = Convert.ToDateTime(dates[0]);
                end = Convert.ToDateTime(dates[1]);
            }
            else if (dates != null && dates.Count == 1)
            {
                start = Convert.ToDateTime(dates[0]);
            }
        }


        /// <summary>
        /// 对于重复标题进行处理
        /// </summary>
        /// <param name="AuthorId"></param>
        /// <param name="ViedoNameSimplify"></param>
        /// <returns></returns>
        public (string, string) GetUperLastViedoFileName(string AuthorId, string ViedoNameSimplify)
        {

            var video = this.Db.Queryable<DouyinVideo>().Where(x => x.AuthorId == AuthorId && x.ViedoType == VideoTypeEnum.dy_follows)
                 .Where(x => x.VideoTitleSimplify == ViedoNameSimplify)
                 .OrderByDescending(x => x.CreateTime).First();

            if (video != null)
            {
                if (string.IsNullOrWhiteSpace(video.VideoTitleSimplifyPrefix))
                {
                    return (ViedoNameSimplify, "001");
                }

                else
                {
                    //VideoTitleSimplifyPrefix的规则是从001开始
                    var prefixNumber = video.VideoTitleSimplifyPrefix.TrimEnd('-');
                    if (int.TryParse(prefixNumber, out int number))
                    {
                        number += 1;
                        return (ViedoNameSimplify, number.ToString("D3"));
                    }
                    else
                    {
                        return (ViedoNameSimplify, "001");
                    }
                }
            }
            else
            {
                return (ViedoNameSimplify, "");
            }
        }

        /// <summary>
        /// 根据视频ID列表获取视频信息
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public async Task<List<DouyinVideo>> GetByIds(List<string> ids)
        {
            // 使用 Queryable 方法构建查询
            return await this.Db.Queryable<DouyinVideo>()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();
        }

        /// <summary>
        /// 记录要重新下载的视频
        /// </summary>
        /// <param name="downs"></param>
        /// <returns></returns>
        public bool InsertReDowns(List<DouyinReDownload> downs)
        {
            if (downs != null)
            {
                return Db.Insertable(downs).ExecuteCommand() > 0;

            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 更新视频重新下载状态
        /// </summary>
        /// <param name="videoId">视频ID（对应ViedoReDown.Id）</param>
        /// <param name="status">状态值（1时会同步更新下载时间）</param>
        /// <returns>是否更新成功（影响行数>0）</returns>
        public async Task<bool> UpdateReDownStatus(string videoId, int status)
        {
            // 校验必填参数（避免无效数据库操作）
            if (string.IsNullOrWhiteSpace(videoId))
            {
                Serilog.Log.Error("更新重新下载状态失败：videoId为空");
                return false;
            }

            // 构建更新条件（统一Where条件，避免重复代码）
            var updateable = Db.Updateable<DouyinReDownload>()
                .Where(it => it.Id == videoId)
                .SetColumns(it => new DouyinReDownload
                {
                    Status = status,
                    UpdateTime = DateTime.Now
                });

            if (status == 1)
            {
                updateable = updateable.SetColumns(it => it.DownTime == DateTime.Now);

            }
            int affectedRows = await updateable.ExecuteCommandAsync();

            // 可选：记录更新结果日志
            if (affectedRows <= 0)
            {
                Serilog.Log.Debug("更新重新下载状态无匹配数据：videoId={0}, status={1}", videoId, status);
            }

            return affectedRows > 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<List<DouyinReDownload>> GetViedoReDowns()
        {
            return await this.Db.Queryable<DouyinReDownload>()
                .Where(x => x.Status == 0 || x.Status == 2)
                .ToListAsync();
        }
    }
}
