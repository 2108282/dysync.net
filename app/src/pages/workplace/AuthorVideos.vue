<template>
  <main class="author-videos-page">

    <section class="author-switcher">
      <button class="switcher-toggle" type="button" :aria-expanded="filterExpanded" @click="filterExpanded = !filterExpanded">
        <span class="switcher-toggle-icon">⌕</span>
        <span>筛选 / 切换博主</span>
        <span class="switcher-current">当前：{{ selectedAuthorName || '未知博主' }}</span>
        <span class="switcher-chevron" :class="{ expanded: filterExpanded }">⌄</span>
      </button>

      <div v-if="filterExpanded" class="switcher-body">
        <label class="author-search">
          <span aria-hidden="true">⌕</span>
          <input v-model="authorSearchInput" type="search" placeholder="输入博主名称搜索" autocomplete="off" />
          <button v-if="authorSearchInput" type="button" aria-label="清空搜索" @click="authorSearchInput = ''">×</button>
        </label>

        <div v-if="authorSearchInput.trim()" class="author-search-results">
          <span v-if="authorSearching" class="search-hint">正在搜索…</span>
          <button
            v-for="author in searchResults"
            :key="author.uperId || author.name"
            type="button"
            class="switch-author search-author"
            :class="{ active: isSelectedAuthor(author) }"
            @click="selectAuthor(author)"
          >
            <img v-if="author.icon" :src="author.icon" :alt="author.name" />
            <span v-else class="switch-avatar-fallback">{{ author.name.slice(0, 1) }}</span>
            <span class="switch-author-name">{{ author.name }}</span>
            <span class="switch-author-count">{{ author.count }}</span>
          </button>
          <span v-if="!authorSearching && !searchResults.length" class="search-hint">没有找到匹配的博主</span>
        </div>

        <div v-else class="switch-author-row" aria-label="视频数量前10位博主">
          <button
            v-for="author in switchAuthors"
            :key="author.uperId || author.name"
            type="button"
            class="switch-author"
            :class="{ active: isSelectedAuthor(author) }"
            :title="`${author.name} · ${author.count} 个视频`"
            @click="selectAuthor(author)"
          >
            <img v-if="author.icon" :src="author.icon" :alt="author.name" />
            <span v-else class="switch-avatar-fallback">{{ author.name.slice(0, 1) }}</span>
            <span class="switch-author-name">{{ author.name }}</span>
            <span class="switch-author-count">{{ author.count }}</span>
          </button>
          <span v-if="!switchAuthors.length && !topAuthorsLoading" class="search-hint">暂无可切换博主</span>
          <a-spin v-if="topAuthorsLoading" size="small" />
        </div>
      </div>
    </section>

    <section class="poster-section" aria-label="博主视频列表">
      <div v-if="loading && videos.length === 0" class="state-panel">
        <a-spin size="large" />
        <span>正在加载视频…</span>
      </div>
      <div v-else-if="!loading && videos.length === 0" class="state-panel empty-state">
        <div class="empty-mark">▧</div>
        <strong>还没有已同步的视频</strong>
        <span>该作者的视频完成同步后会显示在这里</span>
      </div>

      <div v-else class="poster-grid">
        <article
          v-for="video in videos"
          :key="video.id"
          class="poster-card"
          role="button"
          tabindex="0"
          @click="playVideo(video)"
          @keydown.enter="playVideo(video)"
        >
          <div class="poster-image">
            <img
              v-if="video.videoCoverUrl"
              :src="video.videoCoverUrl"
              :alt="video.videoTitle || '视频封面'"
              loading="lazy"
              @error="hideBrokenPoster"
            />
            <div v-else class="poster-placeholder"><span>▶</span></div>
            <span class="poster-type">{{ video.viedoTypeStr || '视频' }}</span>
          </div>
          <div class="poster-copy">
            <h2 :title="video.videoTitle || '无标题'">{{ video.videoTitle || '无标题' }}</h2>
            <p>{{ formatDate(video.createTime) }}</p>
          </div>
        </article>
      </div>

      <div v-if="videos.length > 0" ref="loadMoreRef" class="load-more">
        <a-spin v-if="loading" size="small" />
        <span v-else-if="hasMore">继续向下滚动加载</span>
        <span v-else>已显示全部 {{ total }} 个视频</span>
        <button v-if="hasMore && !loading" type="button" @click="loadVideos">加载更多</button>
      </div>
      <div v-if="errorMessage" class="error-message">{{ errorMessage }}</div>
    </section>

    <div v-if="activeVideo" class="video-player-overlay" @click.self="closePlayer">
      <section class="video-player-dialog" role="dialog" aria-modal="true" :aria-label="activeVideo.videoTitle || '视频播放'">
        <header class="video-player-header">
          <h2>{{ activeVideo.videoTitle || '视频播放' }}</h2>
          <button type="button" aria-label="关闭播放器" @click="closePlayer">×</button>
        </header>
        <div class="video-player-body">
          <video
            v-if="!playError"
            :key="activeVideo.id"
            :src="`/api/video/play/${encodeURIComponent(activeVideo.id)}`"
            controls
            autoplay
            playsinline
            @error="playError = true"
          />
          <p v-else class="play-error">视频文件无法播放，可能尚未下载或文件已移除。</p>
        </div>
      </section>
    </div>
  </main>
</template>

<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useApiStore } from '@/store';

interface AuthorVideo {
  id: string;
  videoTitle?: string;
  videoCoverUrl?: string;
  createTime?: string;
  viedoTypeStr?: string;
}

interface AuthorOption {
  name: string;
  count: number;
  icon: string;
  uperId?: string;
}

const PAGE_SIZE = 48;
const route = useRoute();
const filterExpanded = ref(false);
const topAuthors = ref<AuthorOption[]>([]);
const topAuthorsLoading = ref(false);
const authorSearchInput = ref('');
const searchResults = ref<AuthorOption[]>([]);
const authorSearching = ref(false);
const searchedAuthorKeys = ref(new Set<string>());
const videos = ref<AuthorVideo[]>([]);
const activeVideo = ref<AuthorVideo | null>(null);
const playError = ref(false);
const total = ref(0);
const pageIndex = ref(0);
const loading = ref(false);
const hasMore = ref(true);
const errorMessage = ref('');
const loadMoreRef = ref<HTMLElement | null>(null);
let observer: IntersectionObserver | null = null;
let searchTimer: ReturnType<typeof setTimeout> | undefined;
let searchVersion = 0;
let videosRequestVersion = 0;

const queryValue = (value: unknown) => Array.isArray(value) ? String(value[0] ?? '') : String(value ?? '');
const selectedAuthorId = ref(queryValue(route.query.authorId));
const selectedAuthorName = ref(queryValue(route.query.authorName));
const switchAuthors = computed(() => {
  if (authorSearchInput.value.trim()) return [];
  return topAuthors.value.filter(author => !searchedAuthorKeys.value.has(authorKey(author)));
});

onMounted(async () => {
  document.title = `${selectedAuthorName.value || '博主'} · 视频海报墙`;
  await Promise.all([loadVideos(), loadTopAuthors()]);
  observer = new IntersectionObserver(
    entries => {
      if (entries[0]?.isIntersecting) void loadVideos();
    },
    { rootMargin: '300px 0px' },
  );
  if (loadMoreRef.value) observer.observe(loadMoreRef.value);
});

onUnmounted(() => {
  observer?.disconnect();
  if (searchTimer) clearTimeout(searchTimer);
});

watch(authorSearchInput, keyword => {
  if (searchTimer) clearTimeout(searchTimer);
  const normalizedKeyword = keyword.trim();
  searchResults.value = [];
  if (!normalizedKeyword) {
    authorSearching.value = false;
    return;
  }
  authorSearching.value = true;
  searchTimer = setTimeout(() => void searchAuthors(normalizedKeyword), 250);
});

async function loadVideos() {
  if (loading.value || !hasMore.value) return;
  const requestVersion = ++videosRequestVersion;
  const requestedAuthorId = selectedAuthorId.value;
  const requestedAuthorName = selectedAuthorName.value;
  loading.value = true;
  errorMessage.value = '';
  const nextPage = pageIndex.value + 1;
  try {
    const response = await useApiStore().AuthorVideoPageList(
      nextPage,
      PAGE_SIZE,
      requestedAuthorId,
      requestedAuthorName,
    );
    if (requestVersion !== videosRequestVersion) return;
    if (response?.code !== 0 || !response?.data) {
      throw new Error(response?.message || '获取视频失败');
    }

    const pageVideos = (response.data.data ?? []) as AuthorVideo[];
    videos.value = [...videos.value, ...pageVideos];
    pageIndex.value = nextPage;
    total.value = response.data.total ?? videos.value.length;
    hasMore.value = videos.value.length < total.value && pageVideos.length > 0;
  } catch (error: any) {
    if (requestVersion !== videosRequestVersion) return;
    errorMessage.value = error?.message || error?.msg || '加载视频失败，请稍后重试';
  } finally {
    if (requestVersion === videosRequestVersion) loading.value = false;
  }
}

async function loadTopAuthors() {
  topAuthorsLoading.value = true;
  try {
    const response = await useApiStore().VideoAuthorStatics(1, 10);
    if (response?.code === 0) topAuthors.value = response.data?.data ?? [];
  } catch (error) {
    console.error('加载热门博主失败：', error);
  } finally {
    topAuthorsLoading.value = false;
  }
}

async function searchAuthors(keyword: string) {
  const requestVersion = ++searchVersion;
  try {
    const response = await useApiStore().VideoAuthorOptions(keyword, 10);
    if (requestVersion !== searchVersion || keyword !== authorSearchInput.value.trim()) return;
    const results = (response?.code === 0 ? response.data?.data ?? [] : []) as AuthorOption[];
    searchResults.value = results;
    const nextKeys = new Set(searchedAuthorKeys.value);
    results.forEach(author => nextKeys.add(authorKey(author)));
    searchedAuthorKeys.value = nextKeys;
  } catch (error) {
    if (requestVersion === searchVersion) console.error('搜索博主失败：', error);
  } finally {
    if (requestVersion === searchVersion) authorSearching.value = false;
  }
}

function authorKey(author: AuthorOption) {
  return author.uperId || author.name.trim().toLocaleLowerCase();
}

function isSelectedAuthor(author: AuthorOption) {
  return selectedAuthorId.value
    ? selectedAuthorId.value === author.uperId
    : selectedAuthorName.value === author.name;
}

async function selectAuthor(author: AuthorOption) {
  if (isSelectedAuthor(author)) return;
  videosRequestVersion++;
  selectedAuthorId.value = author.uperId || '';
  selectedAuthorName.value = author.name;
  authorSearchInput.value = '';
  searchResults.value = [];
  videos.value = [];
  total.value = 0;
  pageIndex.value = 0;
  hasMore.value = true;
  loading.value = false;
  errorMessage.value = '';
  document.title = `${author.name} · 视频海报墙`;
  await loadVideos();
}

function playVideo(video: AuthorVideo) {
  activeVideo.value = video;
  playError.value = false;
}

function closePlayer() {
  activeVideo.value = null;
  playError.value = false;
}

function formatDate(value?: string) {
  if (!value) return '日期未知';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '日期未知' : date.toLocaleDateString();
}

function hideBrokenPoster(event: Event) {
  (event.currentTarget as HTMLImageElement).style.opacity = '0';
}
</script>

<style scoped>
.author-videos-page {
  min-height: 100vh;
  padding: 28px clamp(16px, 4vw, 64px) 56px;
  color: #edf3fc;
  background:
    radial-gradient(ellipse at 15% 0%, rgba(48, 104, 177, .2), transparent 36%),
    linear-gradient(145deg, #0c1421, #111b2b 55%, #0b1320);
}

.author-switcher { max-width: 1600px; margin: 0 auto 24px; overflow: hidden; border: 1px solid rgba(180, 203, 235, .12); border-radius: 15px; background: rgba(20, 32, 49, .68); }
.switcher-toggle { display: flex; align-items: center; gap: 10px; width: 100%; min-height: 54px; padding: 0 18px; border: 0; color: #dce8f8; text-align: left; background: transparent; cursor: pointer; }
.switcher-toggle-icon { color: #81baff; font-size: 22px; }
.switcher-current { overflow: hidden; margin-left: 8px; color: #92a4bc; font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }
.switcher-chevron { margin-left: auto; color: #91a8c6; transition: transform .2s ease; }
.switcher-chevron.expanded { transform: rotate(180deg); }
.switcher-body { padding: 0 16px 15px; border-top: 1px solid rgba(180, 203, 235, .08); }
.author-search { display: flex; align-items: center; gap: 10px; max-width: 420px; height: 40px; margin: 14px 0; padding: 0 12px; border: 1px solid rgba(180, 203, 235, .16); border-radius: 10px; color: #82a3cc; background: rgba(5, 13, 24, .38); }
.author-search input { width: 100%; border: 0; outline: 0; color: #edf3fc; background: transparent; }
.author-search input::placeholder { color: #778ba5; }
.author-search > button { border: 0; color: #a9bad0; font-size: 20px; background: transparent; cursor: pointer; }
.switch-author-row, .author-search-results { display: flex; align-items: stretch; gap: 9px; min-height: 68px; overflow-x: auto; padding: 3px 1px 8px; scrollbar-width: thin; }
.switch-author { display: flex; align-items: center; gap: 8px; min-width: 160px; max-width: 220px; padding: 8px 11px; border: 1px solid rgba(180, 203, 235, .1); border-radius: 11px; color: #dce8f8; background: rgba(255, 255, 255, .035); cursor: pointer; }
.switch-author:hover, .switch-author.active { border-color: rgba(91, 163, 255, .55); background: rgba(56, 126, 204, .16); }
.switch-author img, .switch-avatar-fallback { display: grid; place-items: center; width: 34px; height: 34px; flex: 0 0 auto; overflow: hidden; border-radius: 50%; object-fit: cover; background: linear-gradient(135deg, #397fcb, #8a61c5); }
.switch-avatar-fallback { color: white; }
.switch-author-name { overflow: hidden; max-width: 100px; font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }
.switch-author-count { margin-left: auto; color: #91a8c6; font-size: 11px; font-variant-numeric: tabular-nums; white-space: nowrap; }
.search-author { min-width: 190px; }
.search-hint { align-self: center; padding: 0 8px; color: #899bb2; font-size: 12px; white-space: nowrap; }
.poster-section { max-width: 1600px; margin: 0 auto; }
.poster-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(178px, 1fr)); gap: 20px; }
.poster-card { overflow: hidden; border: 1px solid rgba(180, 203, 235, .12); border-radius: 15px; background: #172438; cursor: pointer; transition: transform .2s ease, box-shadow .2s ease; }
.poster-card:hover { transform: translateY(-4px); box-shadow: 0 14px 32px rgba(0, 0, 0, .3); }
.poster-image { position: relative; overflow: hidden; aspect-ratio: 3 / 4; background: linear-gradient(145deg, #263b57, #172438); }
.poster-image img { width: 100%; height: 100%; object-fit: cover; transition: transform .35s ease; }
.poster-card:hover .poster-image img { transform: scale(1.04); }
.poster-placeholder { display: grid; place-items: center; width: 100%; height: 100%; color: rgba(255,255,255,.8); font-size: 36px; background: radial-gradient(circle at 50% 40%, #3a5577, #172438 70%); }
.poster-placeholder span { display: grid; place-items: center; width: 58px; height: 58px; padding-left: 4px; border: 1px solid rgba(255,255,255,.4); border-radius: 50%; }
.poster-type { position: absolute; right: 9px; bottom: 9px; padding: 4px 8px; border: 1px solid rgba(255,255,255,.16); border-radius: 7px; color: #fff; font-size: 11px; background: rgba(6, 13, 23, .68); backdrop-filter: blur(8px); }
.poster-copy { padding: 12px 13px 14px; }
.poster-copy h2 { display: -webkit-box; overflow: hidden; min-height: 40px; margin: 0; color: #edf3fc; font-size: 13px; font-weight: 600; line-height: 1.55; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.poster-copy p { margin: 9px 0 0; color: #8294ad; font-size: 11px; }
.load-more, .state-panel { display: flex; align-items: center; justify-content: center; gap: 12px; min-height: 110px; color: #91a2b9; font-size: 13px; }
.load-more button { padding: 7px 13px; border: 1px solid rgba(155, 184, 220, .25); border-radius: 8px; color: #dce9fa; background: rgba(255,255,255,.06); cursor: pointer; }
.state-panel { flex-direction: column; min-height: 48vh; }
.empty-mark { color: #6883a4; font-size: 48px; }
.state-panel strong { color: #e4edf9; font-size: 17px; }
.state-panel span { color: #8fa1b8; }
.error-message { margin: 0 auto 18px; color: #ff9b9b; text-align: center; }
.video-player-overlay { position: fixed; inset: 0; z-index: 1000; display: grid; place-items: center; padding: 20px; background: rgba(3, 8, 15, .82); backdrop-filter: blur(8px); }
.video-player-dialog { display: flex; flex-direction: column; width: min(960px, 96vw); max-height: 92vh; overflow: hidden; border: 1px solid rgba(180, 203, 235, .18); border-radius: 16px; background: #101b2a; box-shadow: 0 24px 80px rgba(0,0,0,.5); }
.video-player-header { display: flex; align-items: center; gap: 16px; min-height: 52px; padding: 8px 14px 8px 18px; }
.video-player-header h2 { overflow: hidden; flex: 1; margin: 0; color: #edf3fc; font-size: 14px; text-overflow: ellipsis; white-space: nowrap; }
.video-player-header button { width: 34px; height: 34px; border: 0; border-radius: 8px; color: #dce8f8; font-size: 24px; background: rgba(255,255,255,.08); cursor: pointer; }
.video-player-body { display: grid; place-items: center; min-height: 180px; background: #05090f; }
.video-player-body video { display: block; width: 100%; max-height: calc(92vh - 52px); background: #000; }
.play-error { padding: 30px; color: #ffb4b4; text-align: center; }

@media (max-width: 600px) {
  .author-videos-page { padding: 14px 12px 36px; }
    .poster-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
}
</style>
