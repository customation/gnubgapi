/*
 * Simple sanity test for libgnubgapi.
 *
 * Usage:
 *   gnubgapi_test [position_id] [weights] [weights_bin] [match_id]
 */
#include "gnubgapi.h"

#include <stdio.h>

int main(int argc, char **argv) {
    const char *position_id = (argc > 1) ? argv[1] : "ADAAQAkIAAAAAA";
    const char *weights = (argc > 2) ? argv[2] : "../gnubg.weights";
    const char *weights_bin = (argc > 3) ? argv[3] : "../gnubg.wd";
    const char *data_dir = (argc > 4) ? argv[4] : NULL;
    const char *match_id = (argc > 5) ? argv[5] : NULL;

    gnubgapi_context *ctx = gnubgapi_create();
    if (!ctx) {
        fprintf(stderr, "create failed: %s\n", gnubgapi_get_last_error());
        return 1;
    }

    if (gnubgapi_init(ctx, weights, weights_bin, data_dir, 0) != GNUBGAPI_OK) {
        fprintf(stderr, "init failed: %s\n", gnubgapi_get_last_error());
        gnubgapi_destroy(ctx);
        return 1;
    }

    double eq = 0.0;
    double cf = 0.0;
    if (gnubgapi_evaluate_position(ctx, position_id, match_id, &eq, &cf) != GNUBGAPI_OK) {
        fprintf(stderr, "eval failed: %s\n", gnubgapi_get_last_error());
        gnubgapi_shutdown(ctx);
        gnubgapi_destroy(ctx);
        return 1;
    }

    printf("position_id: %s\n", position_id);
    if (match_id && match_id[0]) {
        printf("match_id: %s\n", match_id);
    }
    printf("equity: %.6f\n", eq);
    printf("cubeful_equity: %.6f\n", cf);

    gnubgapi_shutdown(ctx);
    gnubgapi_destroy(ctx);
    return 0;
}
