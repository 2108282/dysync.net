<template>
  <main class="author-videos-page">

    <section class="author-banner">
      <div class="banner-glow banner-glow-one"></div>
      <div class="banner-glow banner-glow-two"></div>
      <div class="author-banner-content">
        <div class="author-banner-avatar">
          <img v-if="bannerAvatar" :src="bannerAvatar" :alt="selectedAuthorName || '博主头像'" />
          <span v-else>{{ (selectedAuthorName || '博').slice(0, 1) }}</span>
        </div>
        <div class="author-banner-info">
          <div class="author-title-row">
            <h1>{{ authorProfile.name || selectedAuthorName || '未知博主' }}</h1>
            <span v-if="authorProfile.enterprise" class="author-verified">{{ authorProfile.enterprise }}</span>
          </div>
          <p class="author-signature">{{ authorProfile.signature || '这个博主还没有填写个人签名' }}</p>
          <div class="author-meta">
            <span v-if="authorProfile.douyinNo">抖音号 {{ authorProfile.douyinNo }}</span>
            <span>{{ total }} 个已同步视频</span>
          </div>
        </div>
        <div class="banner-caption">VIDEO GALLERY</div>
      </div>
    </section>

    <section class="poster-section" aria-label="博主视频列表">
      <div class="poster-toolbar">
        <div class="video-tag-tabs" role="tablist" aria-label="视频分类筛选">
          <button
            type="button"
            class="video-tag-tab"
            :class="{ active: activeVideoTag === null }"
            @click="selectVideoTag(null)"
          >
            全部分类
          </button>
          <button
            v-for="tag in authorVideoTags"
            :key="tag.name"
            type="button"
            class="video-tag-tab"
            :class="{ active: activeVideoTag === tag.name }"
            @click="selectVideoTag(tag.name)"
          >
            {{ tag.name }} <span>{{ tag.count }}</span>
          </button>
        </div>
        <label class="video-title-search">
          <span aria-hidden="true">⌕</span>
          <input v-model="titleSearchInput" type="search" placeholder="搜索当前博主的视频标题" autocomplete="off" />
          <button v-if="titleSearchInput" type="button" aria-label="清空标题搜索" @click="titleSearchInput = ''">×</button>
        </label>
      </div>

      <div v-if="loading && videos.length === 0" class="state-panel">
        <a-spin size="large" />
        <span>正在加载视频…</span>
      </div>
      <div v-else-if="!loading && videos.length === 0" class="state-panel empty-state">
        <div class="empty-mark">▧</div>
        <strong>{{ hasActiveFilter ? '没有符合条件的视频' : '还没有已同步的视频' }}</strong>
        <span>{{ hasActiveFilter ? '试试更换类型或缩短标题关键词' : '该作者的视频完成同步后会显示在这里' }}</span>
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
  authorAvatar?: string;
  authorAvatarUrl?: string;
}

interface AuthorVideoTag {
  name: string;
  count: number;
}

const PAGE_SIZE = 48;
const route = useRoute();
const authorProfile = ref({
  name: '',
  icon: '',
  signature: '',
  enterprise: '',
  douyinNo: '',
});
const videos = ref<AuthorVideo[]>([]);
const authorVideoTags = ref<AuthorVideoTag[]>([]);
const activeVideoTag = ref<string | null>(null);
const titleSearchInput = ref('');
const titleSearchKeyword = ref('');
const activeVideo = ref<AuthorVideo | null>(null);
const playError = ref(false);
const total = ref(0);
const pageIndex = ref(0);
const loading = ref(false);
const hasMore = ref(true);
const errorMessage = ref('');
const loadMoreRef = ref<HTMLElement | null>(null);
let observer: IntersectionObserver | null = null;
let videosRequestVersion = 0;

const queryValue = (value: unknown) => Array.isArray(value) ? String(value[0] ?? '') : String(value ?? '');
const selectedAuthorId = ref(queryValue(route.query.authorId));
const selectedAuthorName = ref(queryValue(route.query.authorName));
const routeAuthorIcon = queryValue(route.query.authorIcon);
const bannerAvatar = computed(() => authorProfile.value.icon || routeAuthorIcon || videos.value[0]?.authorAvatarUrl || videos.value[0]?.authorAvatar || '');
const hasActiveFilter = computed(() => activeVideoTag.value !== null || !!titleSearchKeyword.value);
let titleSearchTimer: ReturnType<typeof setTimeout> | undefined;

onMounted(async () => {
  document.title = `${selectedAuthorName.value || '博主'} · 视频海报墙`;
  await Promise.all([loadVideos(), loadAuthorProfile(), loadAuthorVideoTags()]);
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
  if (titleSearchTimer) clearTimeout(titleSearchTimer);
});

watch(loadMoreRef, element => {
  observer?.disconnect();
  if (element) observer?.observe(element);
});

watch(titleSearchInput, keyword => {
  if (titleSearchTimer) clearTimeout(titleSearchTimer);
  titleSearchTimer = setTimeout(() => {
    titleSearchKeyword.value = keyword.trim();
    resetVideos();
  }, 300);
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
      titleSearchKeyword.value,
      activeVideoTag.value || '',
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

async function loadAuthorVideoTags() {
  try {
    const response = await useApiStore().AuthorVideoTags(
      selectedAuthorId.value,
      selectedAuthorName.value,
    );
    if (response?.code === 0) {
      authorVideoTags.value = (response.data ?? []) as AuthorVideoTag[];
    }
  } catch (error) {
    console.error('加载博主视频分类失败：', error);
  }
}

function selectVideoTag(tag: string | null) {
  if (activeVideoTag.value === tag) return;
  activeVideoTag.value = tag;
  resetVideos();
}

function resetVideos() {
  videosRequestVersion++;
  videos.value = [];
  total.value = 0;
  pageIndex.value = 0;
  hasMore.value = true;
  loading.value = false;
  errorMessage.value = '';
  void loadVideos();
}

async function loadAuthorProfile() {
  try {
    const response = await useApiStore().AuthorVideoProfile(
      selectedAuthorId.value,
      selectedAuthorName.value,
    );
    if (response?.code === 0 && response.data) {
      authorProfile.value = { ...authorProfile.value, ...response.data };
    }
  } catch (error) {
    console.error('加载博主资料失败：', error);
  }
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

.author-banner { position: relative; max-width: 1600px; min-height: 188px; margin: 0 auto 28px; overflow: hidden; border: 1px solid rgba(180, 203, 235, .14); border-radius: 20px; background: linear-gradient(112deg, rgba(25, 42, 64, .98), rgba(18, 30, 47, .88)); box-shadow: 0 20px 54px rgba(0, 0, 0, .18); }
.author-banner-content { position: relative; z-index: 1; display: flex; align-items: center; gap: 24px; min-height: 188px; padding: 26px clamp(20px, 4vw, 48px); }
.banner-glow { position: absolute; width: 280px; height: 280px; border-radius: 50%; filter: blur(2px); pointer-events: none; }
.banner-glow-one { top: -190px; right: 12%; background: rgba(68, 134, 226, .22); }
.banner-glow-two { right: -120px; bottom: -220px; background: rgba(146, 88, 201, .16); }
.author-banner-avatar { display: grid; place-items: center; width: 112px; height: 112px; flex: 0 0 auto; overflow: hidden; border: 3px solid rgba(210, 226, 252, .62); border-radius: 50%; color: #fff; font-size: 38px; background: linear-gradient(135deg, #5494dd, #9b67c8); box-shadow: 0 8px 28px rgba(4, 10, 20, .35); }
.author-banner-avatar img { width: 100%; height: 100%; object-fit: cover; }
.author-banner-info { min-width: 0; }
.author-title-row { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; }
.author-title-row h1 { overflow: hidden; margin: 0; color: #f4f7fc; font-size: clamp(23px, 3vw, 32px); font-weight: 700; letter-spacing: .01em; text-overflow: ellipsis; }
.author-verified { padding: 4px 9px; border: 1px solid rgba(115, 183, 255, .28); border-radius: 999px; color: #aad4ff; font-size: 11px; background: rgba(63, 141, 224, .13); }
.author-signature { overflow: hidden; max-width: min(780px, 75vw); margin: 10px 0 14px; color: #aebed2; font-size: 14px; line-height: 1.6; text-overflow: ellipsis; }
.author-meta { display: flex; flex-wrap: wrap; gap: 9px; }
.author-meta span { padding: 5px 10px; border: 1px solid rgba(177, 200, 232, .12); border-radius: 999px; color: #c5d3e5; font-size: 11px; background: rgba(7, 15, 26, .24); }
.banner-caption { margin-left: auto; color: rgba(173, 197, 228, .57); font-size: 11px; font-weight: 600; letter-spacing: .22em; white-space: nowrap; }
.poster-section { max-width: 1600px; margin: 0 auto; }
.poster-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
.video-tag-tabs { display: flex; align-items: center; gap: 8px; min-width: 0; overflow-x: auto; padding: 3px 1px 7px; scrollbar-width: thin; }
.video-tag-tab { display: inline-flex; align-items: center; gap: 7px; flex: 0 0 auto; min-height: 36px; padding: 0 13px; border: 1px solid rgba(180, 203, 235, .13); border-radius: 999px; color: #a9bad0; font-size: 12px; background: rgba(255, 255, 255, .035); cursor: pointer; transition: border-color .18s ease, background .18s ease, color .18s ease; }
.video-tag-tab:hover, .video-tag-tab.active { border-color: rgba(102, 170, 255, .5); color: #edf5ff; background: rgba(65, 132, 211, .2); }
.video-tag-tab span { color: #8fa8c8; font-size: 10px; font-variant-numeric: tabular-nums; }
.video-title-search { display: flex; align-items: center; gap: 9px; width: min(330px, 34%); min-width: 210px; height: 38px; flex: 0 0 auto; padding: 0 12px; border: 1px solid rgba(180, 203, 235, .15); border-radius: 10px; color: #82a3cc; background: rgba(5, 13, 24, .34); }
.video-title-search input { width: 100%; min-width: 0; border: 0; outline: 0; color: #edf3fc; font-size: 12px; background: transparent; }
.video-title-search input::placeholder { color: #778ba5; }
.video-title-search > button { border: 0; color: #a9bad0; font-size: 19px; background: transparent; cursor: pointer; }
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
.video-player-overlay { position: fixed; inset: 0; z-index: 1000; display: flex; padding: 0; background: #000; }
.video-player-dialog { display: flex; flex-direction: column; width: 100vw; height: 100vh; height: 100dvh; overflow: hidden; border: 0; border-radius: 0; background: #05080d; }
.video-player-header { display: flex; align-items: center; gap: 16px; min-height: 56px; flex: 0 0 56px; padding: 8px 18px; border-bottom: 1px solid rgba(180, 203, 235, .12); background: #101722; }
.video-player-header h2 { overflow: hidden; flex: 1; margin: 0; color: #edf3fc; font-size: 14px; text-overflow: ellipsis; white-space: nowrap; }
.video-player-header button { width: 34px; height: 34px; border: 0; border-radius: 8px; color: #dce8f8; font-size: 24px; background: rgba(255,255,255,.08); cursor: pointer; }
.video-player-body { position: relative; flex: 1 1 auto; min-width: 0; min-height: 0; overflow: hidden; background: #000; }
.video-player-body video { position: absolute; inset: 0; display: block; width: 100%; height: 100%; max-width: 100%; max-height: 100%; background: #000; object-fit: contain; }
.play-error { padding: 30px; color: #ffb4b4; text-align: center; }

@media (max-width: 600px) {
  .author-videos-page { padding: 14px 12px 36px; }
  .author-banner { min-height: 156px; margin-bottom: 20px; border-radius: 16px; }
  .author-banner-content { min-height: 156px; gap: 14px; padding: 20px 16px; }
  .author-banner-avatar { width: 76px; height: 76px; border-width: 2px; font-size: 28px; }
  .author-signature { max-width: 100%; margin: 7px 0 10px; font-size: 12px; }
  .banner-caption { display: none; }
  .poster-toolbar { align-items: stretch; flex-direction: column; gap: 9px; margin-bottom: 14px; }
  .video-title-search { width: 100%; min-width: 0; }
  .poster-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
  .video-player-header { min-height: 50px; flex-basis: 50px; padding: 6px 12px; }
}
</style>
