#!/usr/bin/env python3
import asyncio
import base64
import datetime
import hashlib
import math
import os
import struct
import tempfile

import codetiming
import line_profiler
import numpy as np
import psycopg
import torch
from torch import Tensor

import logger


device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')


@line_profiler.profile
async def load_features() -> tuple[Tensor, Tensor]:
    connection_string = os.getenv('CONNECTION_STRING')
    if not connection_string:
        raise Exception("Missing environment variable CONNECTION_STRING")

    async with await psycopg.AsyncConnection.connect(connection_string) as connection:
        with (codetiming.Timer(initial_text="Calculating tensor size...",
                               text=lambda seconds: f"Tensor size is {(time_count, len(instruments), 3)}, "
                                                    f"calculated in {datetime.timedelta(seconds=int(seconds))}.",
                               logger=logger.info)):
            async with connection.cursor() as cursor:  # client cursor
                await cursor.execute('SELECT DISTINCT instrument FROM feature ORDER BY instrument')
                instruments = [i[0] for i in await cursor.fetchall()]
                #instruments = [8, 1781, 1808, 1824, 1829, 1847, 1848, 1853, 1871, 1884, 1890, 1925, 1931, 1942, 1944, 1946, 1952, 1955, 1960, 1972, 1978, 1983, 1996, 2017, 2018, 2032, 2035, 2036, 2038, 2102, 2108, 2109, 2127, 2136, 2152, 2155, 2188, 2210, 2217, 2226, 2230, 2231, 2232, 2240, 2241, 2276, 2278, 2294, 2319, 2322, 2347, 2378, 2386, 2401, 2433, 2444, 2447, 2450, 2459, 2490, 2499, 2527, 2556, 2570, 2579, 2594, 2625, 2626, 2642, 2646, 2650, 2659, 2665, 2670, 2673, 2685, 2713, 2715, 2736, 2744, 2762, 2778, 2783, 2801, 2808, 2828, 2841, 2850, 2887, 2912, 2915, 2942, 2945, 2948, 2970, 2973, 3009, 3019, 3026, 3030, 3060, 3080, 3092, 3154, 3166, 3172, 3195, 3196, 3199, 3202, 3212, 3220, 3227, 3231, 3240, 3244, 3246, 3252, 3265, 3277, 3278, 3293, 3297, 3300, 3305, 3308, 3316, 3328, 3332, 3353, 3362, 3381, 3382, 3384, 3410, 3417, 3437, 3459, 3469, 3492, 3511, 3550, 3553, 3572, 3574, 3582, 3593, 3602, 3610, 3615, 3637, 3670, 3674, 3678, 3691, 3704, 3722, 4213, 4221, 4223, 4234, 4235, 4240, 4241, 4243, 4245, 4246, 4252]
                time_limit_env = os.getenv('TIME_LIMIT')
                if time_limit_env and time_limit_env.isdigit():
                    time_count = int(time_limit_env)
                else:
                    await cursor.execute('SELECT COUNT(DISTINCT timestamp) FROM feature')
                    # noinspection PyUnresolvedReferences
                    time_count = (await cursor.fetchone())[0]

                instrument_indices = [-1] * instruments[-1]
                for i, instrument in enumerate(instruments):
                    instrument_indices[instrument - 1] = i

        hash_md5 = hashlib.md5(struct.pack(f"<{len(instruments)}h", *instruments))
        hash_base64 = base64.b64encode(hash_md5.digest()[:6]).decode('ascii').replace('/', '+')
        path = os.path.join(tempfile.gettempdir(), f'trading_bot_{time_count}x{len(instruments)}_{hash_base64}.pt')

        if os.path.exists(path):
            with (codetiming.Timer(initial_text=f"Loading features from {path}...",
                                   text=lambda seconds: f"Done in {datetime.timedelta(seconds=int(seconds))}.",
                                   logger=logger.info)):
                tensors = torch.load(path, weights_only=True, map_location=device)
            return tensors['features'], tensors['time']

        with (codetiming.Timer(initial_text="Reading features from DB...",
                               text=lambda seconds: f"Done in {datetime.timedelta(seconds=int(seconds))}.",
                               logger=logger.info)):
            async with connection.cursor('trading_bot_cursor') as cursor:  # server cursor (allows lazy loading)
                # noinspection SpellCheckingInspection
                cursor.itersize = 2000

                # Market data history
                feature_tensor = torch.empty((time_count, len(instruments), 3), dtype=torch.float32, device=device)
                buffer_row_count = int(256 * 1024 / (len(instruments) * 3 * torch.float32.itemsize))  # loading to GPU in 256kB batches
                if buffer_row_count == 0:
                    buffer_row_count = 1
                buffer = np.empty((buffer_row_count, len(instruments), 3), dtype=np.float32)
                # Sine and cosine of the time of day
                time_encoding = np.empty((time_count, 2), dtype=np.float32)

                await cursor.execute('''
                    SELECT
                        timestamp,
                        instrument,
                        lag,
                        gap,
                        volume
                    FROM feature
                    ORDER BY timestamp, instrument
                    LIMIT %s''', (time_count * len(instruments),))

                last_timestamp = -1
                time_index = -1
                buffer_index = -1
                write_index = 0
                async for timestamp, instrument, lag, gap, volume in cursor:
                    if timestamp != last_timestamp:
                        last_timestamp = timestamp
                        time_index += 1
                        buffer_index += 1
                        if time_index == time_count:
                            break
                        if buffer_index == buffer_row_count:
                            feature_tensor[write_index:write_index+buffer_index, :, :] = torch.as_tensor(buffer)  # flush the buffer to the GPU
                            write_index += buffer_row_count
                            buffer_index = 0
                        buffer[buffer_index, :, :] = np.nan  # invalidate the next row
                        time_of_day = 2 * math.pi * (timestamp % 86400) / 86400  # [0, 2π)
                        time_encoding[time_index, 0] = math.sin(time_of_day) * math.sqrt(2)  # [-√2, √2)
                        time_encoding[time_index, 1] = math.cos(time_of_day) * math.sqrt(2)
                    buffer[buffer_index, instrument_indices[instrument - 1], :] = (lag, gap, volume)
                feature_tensor[write_index:write_index+buffer_index, :, :] = torch.as_tensor(buffer[buffer_index, :, :])  # flush the remaining buffer
                time_tensor = torch.as_tensor(time_encoding, device=device)

        with (codetiming.Timer(initial_text=f"Saving features to {path}...",
                               text=lambda seconds: f"Done in {datetime.timedelta(seconds=int(seconds))}.",
                               logger=logger.info)):
            torch.save({'features': feature_tensor, 'time': time_tensor }, path)

        return feature_tensor, time_tensor


async def main():
    with (codetiming.Timer(text=lambda seconds: f"Total running time: {datetime.timedelta(seconds=int(seconds))}.",
                           logger=logger.info)):
        print(f"Using {device}.")
        feature_tensor, time_tensor = await load_features()


if __name__ == '__main__':
    asyncio.run(main())
