#include "gnubgapi.h"
#include <stdio.h>
int main(void) {
    printf("sizeof(gnubgapi_move) = %zu\n", sizeof(gnubgapi_move));
    printf("sizeof(gnubgapi_scored_move) = %zu\n", sizeof(gnubgapi_scored_move));
    printf("offsetof(scored_move, equity) = %zu\n", __builtin_offsetof(gnubgapi_scored_move, equity));
    printf("offsetof(scored_move, probs) = %zu\n", __builtin_offsetof(gnubgapi_scored_move, probs));
    printf("GNUBGAPI_MAX_MOVES = %d\n", GNUBGAPI_MAX_MOVES);
    printf("total buffer = %zu bytes\n", sizeof(gnubgapi_scored_move) * GNUBGAPI_MAX_MOVES);
    return 0;
}
