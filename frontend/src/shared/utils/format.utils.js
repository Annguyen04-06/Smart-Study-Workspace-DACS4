/**
 * Bộ hàm định dạng thời gian tương đối tiếng Việt, dung lượng tệp MB, đồng hồ mm:ss và con số.
 */

export function formatRelativeTime(isoDateString) {
    if (!isoDateString) {
        return '';
    }
    const date = new Date(isoDateString);
    if (Number.isNaN(date.getTime())) {
        return '';
    }
    const diffSeconds = Math.floor((Date.now() - date.getTime()) / 1000);
    if (diffSeconds < 45) {
        return 'Vừa xong';
    }
    const diffMinutes = Math.floor(diffSeconds / 60);
    if (diffMinutes < 60) {
        return `${diffMinutes} phút trước`;
    }
    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) {
        return `${diffHours} giờ trước`;
    }
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) {
        return `${diffDays} ngày trước`;
    }
    return formatDateTime(isoDateString);
}

export function formatDateTime(isoDateString) {
    if (!isoDateString) {
        return '';
    }
    const date = new Date(isoDateString);
    if (Number.isNaN(date.getTime())) {
        return '';
    }
    return new Intl.DateTimeFormat('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    }).format(date);
}

export function formatShortTime(isoDateString) {
    if (!isoDateString) {
        return '';
    }
    const date = new Date(isoDateString);
    if (Number.isNaN(date.getTime())) {
        return '';
    }
    return new Intl.DateTimeFormat('vi-VN', {
        hour: '2-digit',
        minute: '2-digit'
    }).format(date);
}

export function formatFileSize(bytes) {
    const num = Number(bytes) || 0;
    if (num < 1024) {
        return `${num} B`;
    }
    if (num < 1024 * 1024) {
        return `${(num / 1024).toFixed(1)} KB`;
    }
    return `${(num / (1024 * 1024)).toFixed(2)} MB`;
}

export function formatTimerMmSs(totalSeconds) {
    const safeSec = Math.max(0, Math.floor(Number(totalSeconds) || 0));
    const minutes = Math.floor(safeSec / 60);
    const seconds = safeSec % 60;
    return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function formatNumber(num) {
    return new Intl.NumberFormat('vi-VN').format(Number(num) || 0);
}
