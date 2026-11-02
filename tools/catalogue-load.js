import http from 'k6/http';
import { Rate, Trend } from 'k6/metrics';
import exec from 'k6/execution';

const base = (__ENV.CATALOGUE_LOAD_URL || '').replace(/\/$/, '');
if (!/^https?:\/\//.test(base)) throw new Error('Set CATALOGUE_LOAD_URL to the approved Stage origin');
const rejected = new Rate('catalogue_rejected');
const unexpected = new Rate('catalogue_unexpected');
const partial = new Rate('catalogue_partial');
const duration = new Trend('catalogue_duration', true);
const scenario = (rate, duration, startTime, phase) => ({
    executor: 'constant-arrival-rate', rate, timeUnit: '1s', duration, startTime,
    preAllocatedVUs: 50, maxVUs: 50, gracefulStop: '10s', tags: { phase },
});
export const options = {
    scenarios: {
        warmup: scenario(20, '2m', '0s', 'warmup'),
        baseline: scenario(20, '15m', '2m10s', 'baseline'),
        burst: scenario(40, '2m', '17m20s', 'burst'),
        recovery: scenario(20, '30s', '19m30s', 'recovery'),
    },
    thresholds: {
        'catalogue_duration{phase:baseline}': ['p(95)<=2000', 'p(99)<=4000'],
        'catalogue_unexpected{phase:baseline}': ['rate<0.01'],
        'catalogue_rejected{phase:baseline}': ['rate==0'],
        'catalogue_partial{phase:baseline}': ['rate==0'],
        'catalogue_unexpected{phase:burst}': ['rate<0.01'],
        'catalogue_rejected{phase:recovery}': ['rate<0.01'],
        'catalogue_unexpected{phase:recovery}': ['rate<0.01'],
        dropped_iterations: ['count==0'],
    },
};

export default function () {
    const slot = exec.scenario.iterationInTest % 10;
    const city = __ENV.CATALOGUE_LOAD_CITY;
    const origin = `latitude=${__ENV.CATALOGUE_LOAD_LAT || '35.7'}&longitude=${__ENV.CATALOGUE_LOAD_LON || '51.4'}`;
    let path;
    if (slot < 6) {
        const q = slot % 2 ? 'پارک' : 'هتل';
        // One request out of ten exercises the real SSR-to-API path.
        path = `${slot === 0 ? '/catalogue' : '/api/catalogue/items'}?q=${encodeURIComponent(q)}${city ? '&cityId='+encodeURIComponent(city) : ''}`;
    } else if (slot < 8) path = `/api/catalogue/items?location=nearby&${origin}`;
    else if (slot === 8) path = '/api/catalogue/items?page=2';
    else path = '/api/catalogue/filters/locations';
    const response = http.get(base + path, { timeout: '8s', redirects: 0 });
    const limited = response.status === 429;
    let incomplete = false;
    let invalid = response.status !== 200 && !limited;
    if (response.status === 200 && path.startsWith('/api/')) {
        try { const payload = response.json(); incomplete = payload.data?.isPartial === true; invalid ||= payload.success !== true || payload.data == null; }
        catch { invalid = true; }
    }
    rejected.add(limited);
    unexpected.add(invalid);
    partial.add(incomplete);
    duration.add(response.timings.duration);
}

export function handleSummary(data) {
    return { 'catalogue-load-summary.json': JSON.stringify({
        commit: __ENV.CATALOGUE_RELEASE_COMMIT || null,
        image: __ENV.CATALOGUE_RELEASE_IMAGE || null,
        ...data,
    }, null, 2) };
}
