/*
 * Test gnubgapi_generate_moves_with_eval to diagnose crash.
 */
#include "gnubgapi.h"
#include <stdio.h>
#include <string.h>

int main(void) {
    const char *position_id = "4HPwATDgc/ABMA";  /* starting position */
    const char *match_id = "cAkAAAAAAAAA";        /* money game */
    const char *data_dir = "C:\\git\\github\\customation\\gnubgapi\\data";

    printf("Creating context...\n");
    gnubgapi_context *ctx = gnubgapi_create();
    if (!ctx) {
        fprintf(stderr, "create failed: %s\n", gnubgapi_get_last_error());
        return 1;
    }

    printf("Initializing with data_dir=%s...\n", data_dir);
    char weights[512], weights_bin[512];
    snprintf(weights, sizeof(weights), "%s\\gnubg.weights", data_dir);
    snprintf(weights_bin, sizeof(weights_bin), "%s\\gnubg.wd", data_dir);

    if (gnubgapi_init(ctx, weights, weights_bin, data_dir, 0) != GNUBGAPI_OK) {
        fprintf(stderr, "init failed: %s\n", gnubgapi_get_last_error());
        gnubgapi_destroy(ctx);
        return 1;
    }

    /* First test: evaluate (this works) */
    printf("Evaluating position...\n");
    double eq = 0.0, cf = 0.0;
    if (gnubgapi_evaluate_position(ctx, position_id, match_id, &eq, &cf) != GNUBGAPI_OK) {
        fprintf(stderr, "eval failed: %s\n", gnubgapi_get_last_error());
    } else {
        printf("equity=%.6f cubeful=%.6f\n", eq, cf);
    }

    /* Second test: generate moves with eval (this crashes in C#) */
    printf("Generating moves with eval (die1=3, die2=1, plies=0)...\n");
    gnubgapi_scored_move moves[GNUBGAPI_MAX_MOVES];
    memset(moves, 0, sizeof(moves));
    uint32_t count = 0;

    gnubgapi_status st = gnubgapi_generate_moves_with_eval(
        ctx, position_id, match_id, 3, 1, 0, moves, &count);

    if (st != GNUBGAPI_OK) {
        fprintf(stderr, "generate_moves_with_eval failed: %s\n", gnubgapi_get_last_error());
    } else {
        printf("Got %u moves\n", count);
        for (uint32_t i = 0; i < count && i < 5; i++) {
            printf("  %u: equity=%.4f  move=", i+1, moves[i].equity);
            for (int j = 0; j < 8 && moves[i].move.an_move[j] >= 0; j += 2) {
                printf("%d/%d ", moves[i].move.an_move[j]+1, moves[i].move.an_move[j+1]+1);
            }
            printf("\n");
        }
    }

    printf("Shutting down...\n");
    gnubgapi_shutdown(ctx);
    gnubgapi_destroy(ctx);
    printf("Done.\n");
    return 0;
}
